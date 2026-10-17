using SkiaSharp;

namespace ServoERP.Api.Mobile;

/// <summary>Decodes bounded field photos and emits a fresh, metadata-free JPEG.</summary>
public static class MobilePhotoProcessor
{
    public static byte[] Normalize(byte[] source)
    {
        if (source.Length is 0 or > 10 * 1024 * 1024)
            throw new MobileHttpException(400, "Choose a JPEG or PNG photo under 10 MB.");
        using var stream = new SKMemoryStream(source);
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png))
            throw new MobileHttpException(400, "Only valid JPEG and PNG photos are supported.");
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || info.Width > 10000 || info.Height > 10000 || (long)info.Width * info.Height > 30_000_000)
            throw new MobileHttpException(400, "This photo is too large. Choose a smaller image.");
        var scale = Math.Min(1f, 1600f / Math.Max(info.Width, info.Height));
        var dimensions = codec.GetScaledDimensions(scale);
        using var decoded = new SKBitmap(new SKImageInfo(dimensions.Width, dimensions.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        if (codec.GetPixels(decoded.Info, decoded.GetPixels()) != SKCodecResult.Success)
            throw new MobileHttpException(400, "This photo could not be read. Choose a valid JPEG or PNG photo.");
        int width = Math.Max(1, (int)Math.Round(info.Width * scale));
        int height = Math.Max(1, (int)Math.Round(info.Height * scale));
        bool swap = codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        using var output = new SKBitmap(swap ? height : width, swap ? width : height);
        using (var canvas = new SKCanvas(output))
        using (var paint = new SKPaint())
        {
            canvas.Clear(SKColors.White);
            // EXIF orientation is applied to pixels; encoded output retains no source metadata.
            switch (codec.EncodedOrigin)
            {
                case SKEncodedOrigin.TopRight: canvas.Translate(width, 0); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.BottomRight: canvas.Translate(width, height); canvas.RotateDegrees(180); break;
                case SKEncodedOrigin.BottomLeft: canvas.Translate(0, height); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.LeftTop: canvas.RotateDegrees(90); canvas.Scale(1, -1); break;
                case SKEncodedOrigin.RightTop: canvas.Translate(height, 0); canvas.RotateDegrees(90); break;
                case SKEncodedOrigin.RightBottom: canvas.Translate(height, width); canvas.RotateDegrees(90); canvas.Scale(-1, 1); break;
                case SKEncodedOrigin.LeftBottom: canvas.Translate(0, width); canvas.RotateDegrees(270); break;
            }
            using var decodedImage = SKImage.FromBitmap(decoded);
            canvas.DrawImage(decodedImage, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear), paint);
        }
        using var image = SKImage.FromBitmap(output);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 82);
        return jpeg.ToArray();
    }
}
