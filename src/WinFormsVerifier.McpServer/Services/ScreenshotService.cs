using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FlaUI.Core.AutomationElements;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Services;

public class ScreenshotResult
{
    public byte[] Bytes { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = "image/png";
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int ScaledWidth { get; set; }
    public int ScaledHeight { get; set; }
    public string? TargetName { get; set; }

    public string Describe()
    {
        var target = TargetName ?? "Giao diện";
        if (OriginalWidth == ScaledWidth && OriginalHeight == ScaledHeight)
        {
            return $"Ảnh chụp '{target}': {OriginalWidth}x{OriginalHeight} ({MimeType}).";
        }
        return $"Ảnh chụp '{target}': Kích thước gốc {OriginalWidth}x{OriginalHeight} -> đã scale còn {ScaledWidth}x{ScaledHeight} ({MimeType}).";
    }
}

public sealed class ScreenshotService
{
    private const int MaxByteSize = 4 * 1024 * 1024; // 4MB

    public ScreenshotResult Capture(
        AutomationElement element,
        int maxWidth = 1200,
        string format = "png",
        int quality = 80)
    {
        var envDisabled = Environment.GetEnvironmentVariable("WFVERIFY_DISABLE_SCREENSHOT");
        if (envDisabled == "1" || string.Equals(envDisabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new ToolException(
                ErrorCode.PathDenied,
                "Chức năng chụp ảnh màn hình đã bị vô hiệu hóa bởi cấu hình bảo mật WFVERIFY_DISABLE_SCREENSHOT.");
        }

        Bitmap rawBitmap;
        try
        {
            rawBitmap = element.Capture();
        }
        catch (Exception ex)
        {
            throw new ToolException(
                ErrorCode.Internal,
                $"Không thể chụp ảnh control/cửa sổ: {ex.Message}",
                "Hãy đảm bảo cửa sổ đang hiển thị trên màn hình và không bị lock screen.");
        }

        using (rawBitmap)
        {
            int origW = rawBitmap.Width;
            int origH = rawBitmap.Height;

            if (origW <= 0 || origH <= 0)
            {
                throw new ToolException(ErrorCode.Internal, "Ảnh chụp có kích thước 0x0.");
            }

            var (finalBytes, mime, scaledW, scaledH) = ProcessAndEncode(rawBitmap, maxWidth, format, quality);

            // If still > 4MB, scale down further to 800px
            if (finalBytes.Length > MaxByteSize && scaledW > 800)
            {
                (finalBytes, mime, scaledW, scaledH) = ProcessAndEncode(rawBitmap, 800, "jpeg", Math.Min(quality, 75));
            }

            return new ScreenshotResult
            {
                Bytes = finalBytes,
                MimeType = mime,
                OriginalWidth = origW,
                OriginalHeight = origH,
                ScaledWidth = scaledW,
                ScaledHeight = scaledH,
                TargetName = !string.IsNullOrEmpty(element.Name) ? element.Name : element.AutomationId
            };
        }
    }

    private static (byte[] Bytes, string MimeType, int Width, int Height) ProcessAndEncode(
        Bitmap original,
        int maxWidth,
        string format,
        int quality)
    {
        int targetW = original.Width;
        int targetH = original.Height;

        if (targetW > maxWidth)
        {
            targetH = (int)Math.Round((double)original.Height * maxWidth / original.Width);
            targetW = maxWidth;
        }

        using var destBitmap = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(destBitmap))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(original, 0, 0, targetW, targetH);
        }

        using var ms = new MemoryStream();
        string mime;

        if (string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase))
        {
            mime = "image/jpeg";
            var encoder = GetEncoder(ImageFormat.Jpeg);
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
            destBitmap.Save(ms, encoder, encoderParams);
        }
        else
        {
            mime = "image/png";
            destBitmap.Save(ms, ImageFormat.Png);
        }

        return (ms.ToArray(), mime, targetW, targetH);
    }

    private static ImageCodecInfo GetEncoder(ImageFormat format)
    {
        var codecs = ImageCodecInfo.GetImageEncoders();
        return codecs.FirstOrDefault(codec => codec.FormatID == format.Guid) ?? codecs[0];
    }
}
