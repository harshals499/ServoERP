using SkiaSharp;

namespace ServoERP.Api.Mobile;

public static class MobilePhoto
{
    // Re-encode pixels only, removing GPS and other source metadata from private job photos.
    public static byte[] Normalize(byte[] bytes)
    {
        using var data=SKData.CreateCopy(bytes);using var codec=SKCodec.Create(data);
        if(codec==null||codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png))throw new MobileHttpException(400,"Choose a valid JPEG or PNG photo.");
        int w=codec.Info.Width,h=codec.Info.Height;
        if(w<=0||h<=0||w>10000||h>10000||(long)w*h>30_000_000)throw new MobileHttpException(400,"This photo is too large. Choose a smaller image.");
        using var bitmap=new SKBitmap(new SKImageInfo(w,h,SKColorType.Rgba8888,SKAlphaType.Premul));
        if(codec.GetPixels(bitmap.Info,bitmap.GetPixels())!=SKCodecResult.Success)throw new MobileHttpException(400,"This photo is damaged. Choose another image.");
        bool transpose=codec.EncodedOrigin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        int ow=transpose?h:w,oh=transpose?w:h;float scale=Math.Min(1f,1600f/Math.Max(ow,oh));
        using var target=new SKBitmap(Math.Max(1,(int)Math.Round(ow*scale)),Math.Max(1,(int)Math.Round(oh*scale)));
        using var canvas=new SKCanvas(target);canvas.Clear(SKColors.White);canvas.Scale(scale);
        var matrix=codec.EncodedOrigin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1,0,w,0,1,0,0,0,1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1,0,w,0,-1,h,0,0,1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1,0,0,0,-1,h,0,0,1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0,1,0,1,0,0,0,0,1),
            SKEncodedOrigin.RightTop => new SKMatrix(0,-1,h,1,0,0,0,0,1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0,-1,h,-1,0,w,0,0,1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0,1,0,-1,0,w,0,0,1),
            _ => SKMatrix.Identity
        };
        canvas.Concat(matrix);using var paint=new SKPaint{IsAntialias=true};canvas.DrawBitmap(bitmap,0,0,paint);canvas.Flush();
        using var image=SKImage.FromBitmap(target);using var encoded=image.Encode(SKEncodedImageFormat.Jpeg,82);return encoded.ToArray();
    }
}
