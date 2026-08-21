using System.Diagnostics;
using FlaUI.Core;
using Microsoft.Extensions.Logging.Abstractions;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Services;
using WinFormsVerifier.Tools;
using Xunit;

namespace WinFormsVerifier.IntegrationTests;

/// <summary>
/// Bảo vệ luồng làm việc với ứng dụng cần đăng nhập / chọn SQL thủ công:
/// người dùng tự mở app, agent attach vào, và tuyệt đối không được giết tiến trình đó.
/// </summary>
public class AttachLifecycleTests
{
    private static string ResolveSampleExe()
    {
        var exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp", "bin", "Debug", "net8.0-windows", "SampleApp.exe"));
        return File.Exists(exe) ? exe : Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp\bin\Debug\net8.0-windows\SampleApp.exe");
    }

    [Fact]
    public async Task CloseApp_RefusesToKillProcessNotLaunchedByServer()
    {
        var sampleExe = ResolveSampleExe();
        Assert.True(File.Exists(sampleExe), $"SampleApp.exe not found at: {sampleExe}");

        // Người dùng tự khởi chạy — KHÔNG qua server.
        using var manual = Process.Start(new ProcessStartInfo(sampleExe) { UseShellExecute = false })!;
        try
        {
            manual.WaitForInputIdle(10000);

            using var uiThread = new UiThread();
            using var session = new UiSession(uiThread, NullLogger<UiSession>.Instance);
            session.SetSession(Application.Attach(manual.Id), launchedByUs: false);

            var result = await AppLifecycleTools.CloseApp(session);

            Assert.True(result.IsError, "wf_close_app phải từ chối đóng ứng dụng không do server khởi chạy.");
            Assert.False(manual.HasExited, "Ứng dụng của người dùng KHÔNG được phép bị giết.");
            Assert.NotNull(session.App);

            // wf_detach_app mới là cách đúng để kết thúc phiên.
            var detach = await AppLifecycleTools.DetachApp(session);
            Assert.False(detach.IsError);
            Assert.Null(session.App);

            manual.Refresh();
            Assert.False(manual.HasExited, "Sau detach, ứng dụng vẫn phải còn chạy.");
        }
        finally
        {
            try { if (!manual.HasExited) manual.Kill(true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void FindProcessesByWindowTitle_MatchesLoginWindow_NotJustMainWindowTitle()
    {
        var sampleExe = ResolveSampleExe();
        using var manual = Process.Start(new ProcessStartInfo(sampleExe) { UseShellExecute = false })!;
        try
        {
            manual.WaitForInputIdle(10000);
            Thread.Sleep(700);

            var matches = NativeWindows.FindProcessesByWindowTitle("Đăng nhập");

            Assert.True(matches.ContainsKey(manual.Id),
                $"Phải tìm được PID {manual.Id} qua tiêu đề cửa sổ đăng nhập. Tìm thấy: [{string.Join(", ", matches.Select(m => $"{m.Key}:{m.Value}"))}]");
        }
        finally
        {
            try { if (!manual.HasExited) manual.Kill(true); } catch { /* ignore */ }
        }
    }
}
