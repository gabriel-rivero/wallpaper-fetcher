// Cover-fit crop/resize so every image exactly fills its target monitor's resolution with no letterboxing.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WallpaperFetcher;

public static class ImageProcessor
{
    public static void CropResizeToFill(string sourcePath, string destPath, int targetWidth, int targetHeight)
    {
        using var src = Image.FromFile(sourcePath);

        var scale = Math.Max((double)targetWidth / src.Width, (double)targetHeight / src.Height);
        var scaledWidth = Math.Max(1, (int)Math.Ceiling(src.Width * scale));
        var scaledHeight = Math.Max(1, (int)Math.Ceiling(src.Height * scale));

        using var scaled = new Bitmap(scaledWidth, scaledHeight);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, 0, 0, scaledWidth, scaledHeight);
        }

        var cropX = Math.Max(0, (scaledWidth - targetWidth) / 2);
        var cropY = Math.Max(0, (scaledHeight - targetHeight) / 2);

        using var cropped = new Bitmap(targetWidth, targetHeight);
        using (var g = Graphics.FromImage(cropped))
        {
            g.DrawImage(
                scaled,
                new Rectangle(0, 0, targetWidth, targetHeight),
                new Rectangle(cropX, cropY, targetWidth, targetHeight),
                GraphicsUnit.Pixel);
        }

        var jpegEncoder = ImageCodecInfo.GetImageEncoders().First(c => c.FormatID == ImageFormat.Jpeg.Guid);
        using var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(Encoder.Quality, 92L);
        cropped.Save(destPath, jpegEncoder, encParams);
    }
}
