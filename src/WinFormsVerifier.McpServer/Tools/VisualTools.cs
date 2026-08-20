using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services;

namespace WinFormsVerifier.Tools;

[McpServerToolType]
public static class VisualTools
{
    [McpServerTool(Name = "wf_screenshot")]
    [Description("""
        Chụp ảnh màn hình của toàn bộ cửa sổ hoặc một control cụ thể để đưa vào Vision Model thẩm định trực quan.
        Ảnh được tự động scale giữ tỷ lệ nếu vượt quá maxWidth, và nén định dạng PNG/JPEG để dung lượng luôn dưới 4MB.
        Trả về ImageContentBlock kèm TextContentBlock mô tả kích thước thực tế.
        """)]
    public static async Task<CallToolResult> Screenshot(
        UiSession session,
        ElementLocator locator,
        ScreenshotService shots,
        [Description("Selector của control cần chụp (tùy chọn). Nếu bỏ trống sẽ chụp toàn bộ cửa sổ.")]
        string? selector = null,
        [Description("Selector của cửa sổ mục tiêu (tùy chọn).")]
        string? windowSelector = null,
        [Description("Chiều rộng tối đa (pixel) của ảnh. Mặc định 1200px.")]
        int maxWidth = 1200,
        [Description("Định dạng ảnh: 'png' hoặc 'jpeg'. Mặc định 'png'.")]
        string format = "png",
        [Description("Chất lượng nén nếu dùng định dạng jpeg (1-100). Mặc định 80.")]
        int quality = 80,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var shot = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var target = !string.IsNullOrWhiteSpace(selector)
                    ? locator.Resolve(window, selector, TimeSpan.FromSeconds(5))
                    : window;

                return shots.Capture(target, maxWidth, format, quality);
            }, TimeSpan.FromSeconds(20), ct);

            return new CallToolResult
            {
                IsError = false,
                Content =
                {
                    new TextContentBlock { Text = shot.Describe() },
                    new ImageContentBlock { Data = shot.Bytes, MimeType = shot.MimeType }
                }
            };
        });
    }
}
