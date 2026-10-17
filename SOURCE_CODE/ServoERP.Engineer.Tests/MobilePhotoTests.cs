using ServoERP.Api.Mobile;
using SkiaSharp;
using Xunit;

namespace ServoERP.Engineer.Tests;

public sealed class MobilePhotoTests
{
    private static byte[] Photo(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 100);
        return encoded.ToArray();
    }

    [Theory]
    [InlineData(SKEncodedImageFormat.Png)]
    [InlineData(SKEncodedImageFormat.Jpeg)]
    public void AcceptedPhotosBecomeBoundedWhiteBackgroundJpeg(SKEncodedImageFormat format)
    {
        var result = MobilePhotoProcessor.Normalize(Photo(2000, 1000, format));
        using var codec = SKCodec.Create(new SKMemoryStream(result));
        Assert.Equal(SKEncodedImageFormat.Jpeg, codec.EncodedFormat);
        Assert.Equal(1600, codec.Info.Width); Assert.Equal(800, codec.Info.Height);
        Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
        if (format == SKEncodedImageFormat.Png)
        {
            using var decoded = SKBitmap.Decode(result);
            Assert.True(decoded.GetPixel(800, 400).Red > 245);
        }
    }

    [Fact]
    public void InvalidUnsupportedTruncatedAndOversizedPhotosAreClientErrors()
    {
        foreach (var source in new[] { Array.Empty<byte>(), new byte[] { 1, 2, 3 },
            Photo(2, 2, SKEncodedImageFormat.Webp), Photo(10001, 1, SKEncodedImageFormat.Png), new byte[10 * 1024 * 1024 + 1] })
            Assert.Equal(400, Assert.Throws<MobileHttpException>(() => MobilePhotoProcessor.Normalize(source)).Status);
        var jpeg = Photo(100, 100, SKEncodedImageFormat.Jpeg);
        Assert.Equal(400, Assert.Throws<MobileHttpException>(() => MobilePhotoProcessor.Normalize(jpeg[..(jpeg.Length / 2)])).Status);
    }

    [Theory]
    [InlineData(1, 20, 10)] [InlineData(2, 20, 10)] [InlineData(3, 20, 10)] [InlineData(4, 20, 10)]
    [InlineData(5, 10, 20)] [InlineData(6, 10, 20)] [InlineData(7, 10, 20)] [InlineData(8, 10, 20)]
    public void ExifOrientationIsAppliedAndRemoved(int orientation, int width, int height)
    {
        // Little-endian TIFF EXIF with a single orientation tag, injected after JPEG SOI.
        byte[] exif = [0xff, 0xe1, 0, 34, 69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0,
            1, 0, 0x12, 1, 3, 0, 1, 0, 0, 0, (byte)orientation, 0, 0, 0, 0, 0, 0, 0];
        using var source = new SKBitmap(20, 10);
        using (var canvas = new SKCanvas(source))
        using (var red = new SKPaint { Color = SKColors.Red })
        {
            canvas.Clear(SKColors.Blue);
            canvas.DrawRect(0, 0, 10, 5, red);
        }
        using var image = SKImage.FromBitmap(source);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        var jpeg = encoded.ToArray();
        var result = MobilePhotoProcessor.Normalize([..jpeg[..2], ..exif, ..jpeg[2..]]);
        using var codec = SKCodec.Create(new SKMemoryStream(result));
        Assert.Equal(width, codec.Info.Width); Assert.Equal(height, codec.Info.Height);
        Assert.Equal(SKEncodedOrigin.TopLeft, codec.EncodedOrigin);
        Assert.DoesNotContain("Exif", System.Text.Encoding.Latin1.GetString(result));
        using var pixels = SKBitmap.Decode(result);
        var corner = orientation switch
        {
            2 or 6 => (width - 3, 2), 3 or 7 => (width - 3, height - 3),
            4 or 8 => (2, height - 3), _ => (2, 2)
        };
        var color = pixels.GetPixel(corner.Item1, corner.Item2);
        Assert.True(color.Red > 180 && color.Blue < 100, $"Orientation {orientation} placed red incorrectly: {color}");
    }
}
