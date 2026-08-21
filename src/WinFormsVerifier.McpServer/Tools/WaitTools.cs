using System.ComponentModel;
using FlaUI.Core.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services;

namespace WinFormsVerifier.Tools;

[McpServerToolType]
public static class WaitTools
{
    [McpServerTool(Name = "wf_wait_for")]
    [Description("""
        Chờ đợi một control đạt tới một trạng thái nhất định:
        - 'exists': Element tồn tại trên cây UI
        - 'visible': Element không bị offscreen
        - 'enabled': Element đang ở trạng thái Enabled
        - 'gone': Element biến mất khỏi cây UI hoặc chuyển sang offscreen
        """)]
    public static async Task<CallToolResult> WaitFor(
        UiSession session,
        ElementLocator locator,
        [Description("Selector của control cần chờ.")]
        string selector,
        [Description("Trạng thái mong đợi: 'exists', 'visible', 'enabled', 'gone'. Mặc định 'visible'.")]
        string state = "visible",
        [Description("Thời gian chờ tối đa (ms). Mặc định 10000ms.")]
        int timeoutMs = 10000,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var matched = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var timeout = TimeSpan.FromMilliseconds(timeoutMs);

                var success = Retry.WhileFalse(() =>
                {
                    try
                    {
                        var elements = locator.ResolveAll(window, selector, 1);
                        var el = elements.Count > 0 ? elements[0] : null;

                        return state.ToLowerInvariant() switch
                        {
                            "exists" => el != null,
                            "visible" => el != null && !el.SafeIsOffscreen(),
                            "enabled" => el != null && el.SafeIsEnabled(),
                            "gone" => el == null || el.SafeIsOffscreen(),
                            _ => el != null
                        };
                    }
                    catch
                    {
                        return state.Equals("gone", StringComparison.OrdinalIgnoreCase);
                    }
                }, timeout, TimeSpan.FromMilliseconds(200)).Result;

                if (!success)
                {
                    throw new ToolException(
                        ErrorCode.Timeout,
                        $"Hết thời gian chờ {timeoutMs}ms: Control '{selector}' không đạt trạng thái '{state}'.",
                        "Kiểm tra lại xem selector có đúng không hoặc app có đang bị treo không.");
                }

                return true;
            }, TimeSpan.FromMilliseconds(timeoutMs + 3000), ct);

            return McpResults.Ok(new
            {
                selector = selector,
                state = state,
                satisfied = matched
            });
        });
    }

    [McpServerTool(Name = "wf_wait_idle")]
    [Description("""
        Chờ đợi ứng dụng hoàn tất các tác vụ nền và message queue trở về trạng thái rảnh (Idle / WaitWhileBusy).
        Dùng tool này sau khi kích hoạt các tác vụ nặng (load dữ liệu, submit form) trước khi thực hiện thao tác tiếp theo.
        """)]
    public static async Task<CallToolResult> WaitIdle(
        UiSession session,
        [Description("Thời gian chờ tối đa (ms). Mặc định 5000ms.")]
        int timeoutMs = 5000,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            session.EnsureAlive();

            await session.RunAsync(() =>
            {
                if (session.App != null)
                {
                    session.App.WaitWhileBusy(TimeSpan.FromMilliseconds(timeoutMs));
                }
                return true;
            }, TimeSpan.FromMilliseconds(timeoutMs + 2000), ct);

            return McpResults.Ok(new { status = "idle", waitedMs = timeoutMs });
        });
    }
}
