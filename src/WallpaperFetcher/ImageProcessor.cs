// Cover-fit crop/resize so every image exactly fills its target monitor's resolution with no letterboxing.
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WallpaperFetcher;

public static class ImageProcessor
{
    // Perceived-luminance average (0=black..255=white), used to check a candidate wallpaper's
    // thumbnail actually looks dark/light before spending bandwidth on the full-res download.
    public static double ComputeAverageBrightness(byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        using var loaded = new Bitmap(ms);
        using var bmp = loaded.Clone(new Rectangle(0, 0, loaded.Width, loaded.Height), PixelFormat.Format24bppRgb);

        var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            var buffer = new byte[data.Stride * bmp.Height];
            Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            long sum = 0;
            long count = 0;
            for (var y = 0; y < bmp.Height; y++)
            {
                var rowStart = y * data.Stride;
                for (var x = 0; x < bmp.Width; x++)
                {
                    var idx = rowStart + x * 3;
                    byte b = buffer[idx], g = buffer[idx + 1], r = buffer[idx + 2];
                    sum += (long)(0.299 * r + 0.587 * g + 0.114 * b);
                    count++;
                }
            }
            return count == 0 ? 128.0 : (double)sum / count;
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

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
