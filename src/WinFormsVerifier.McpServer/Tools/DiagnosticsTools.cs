using System.ComponentModel;
using System.Runtime.InteropServices;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Services;

namespace WinFormsVerifier.Tools;

[McpServerToolType]
public static class DiagnosticsTools
{
    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [McpServerTool(Name = "wf_ping")]
    [Description("""
        Kiểm tra trạng thái hoạt động (Health Check) của WinForms Verifier MCP Server.
        Trả về thông tin phiên bản server, trạng thái session ứng dụng hiện tại, và DPI scale của màn hình chính.
        Dùng tool này đầu tiên để kiểm tra kết nối MCP và môi trường desktop.
        """)]
    public static async Task<CallToolResult> Ping(
        UiSession session,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            double dpiScale = 1.0;
            try
            {
                var dpi = GetDpiForSystem();
                if (dpi > 0)
                {
                    dpiScale = Math.Round(dpi / 96.0, 2);
                }
            }
            catch
            {
                dpiScale = 1.0;
            }

            var sessionInfo = new
            {
                hasActiveSession = session.App != null && !session.App.HasExited,
                processId = session.ProcessId,
                processName = session.ProcessName,
                launchedByUs = session.LaunchedByUs
            };

            var data = new
            {
                server = "WinFormsVerifier.McpServer",
                version = "1.0.0",
                status = "healthy",
                os = Environment.OSVersion.ToString(),
                runtime = Environment.Version.ToString(),
                primaryMonitorDpiScale = dpiScale,
                session = sessionInfo,
                timestamp = DateTimeOffset.UtcNow
            };

            return await Task.FromResult(McpResults.Ok(data));
        });
    }
}
