using System.ComponentModel;
using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services;

namespace WinFormsVerifier.Tools;

[McpServerToolType]
public static class AppLifecycleTools
{
    [McpServerTool(Name = "wf_launch_app")]
    [Description("""
        Khởi chạy một ứng dụng Windows Forms (.exe) từ đường dẫn cục bộ và chờ cửa sổ chính sẵn sàng.
        Đường dẫn exe phải thuộc danh sách whitelist trong WFVERIFY_ALLOWED_ROOTS.
        Khi khởi chạy thành công, server sẽ quản lý session và tự động dọn dẹp (kill process) khi tắt server.
        """)]
    public static async Task<CallToolResult> LaunchApp(
        UiSession session,
        [Description("Đường dẫn tuyệt đối hoặc tương đối tới file .exe của ứng dụng WinForms.")]
        string exePath,
        [Description("Danh sách tham số dòng lệnh truyền cho ứng dụng (tùy chọn).")]
        string[]? arguments = null,
        [Description("Thư mục làm việc (Working Directory). Mặc định là thư mục chứa file .exe.")]
        string? workingDir = null,
        [Description("Thời gian tối đa (ms) để chờ cửa sổ chính xuất hiện. Mặc định 15000ms.")]
        int waitForWindowMs = 15000,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var fullExePath = PathGuard.ValidateAndNormalize(exePath, nameof(exePath));
            if (!File.Exists(fullExePath))
            {
                throw new ToolException(ErrorCode.PathDenied, $"Không tìm thấy file thực thi tại '{fullExePath}'.");
            }

            var dir = !string.IsNullOrWhiteSpace(workingDir)
                ? PathGuard.ValidateAndNormalize(workingDir, nameof(workingDir))
                : Path.GetDirectoryName(fullExePath)!;

            var psi = new ProcessStartInfo
            {
                FileName = fullExePath,
                WorkingDirectory = dir,
                UseShellExecute = false
            };

            if (arguments != null && arguments.Length > 0)
            {
                foreach (var arg in arguments)
                {
                    psi.ArgumentList.Add(arg);
                }
            }

            var app = Application.Launch(psi);
            session.SetSession(app, launchedByUs: true);

            var window = await session.RunAsync(() =>
            {
                var mainWin = Retry.WhileNull(
                    () =>
                    {
                        try { return app.GetMainWindow(session.Automation); }
                        catch { return null; }
                    },
                    TimeSpan.FromMilliseconds(waitForWindowMs),
                    TimeSpan.FromMilliseconds(250)).Result;

                return mainWin;
            }, TimeSpan.FromMilliseconds(waitForWindowMs + 5000), ct);

            var windowTitle = window is null ? "(Chưa có tiêu đề)" : (window.SafeName() is { Length: > 0 } n ? n : "(Chưa có tiêu đề)");

            return McpResults.Ok(new
            {
                processId = app.ProcessId,
                processName = Process.GetProcessById(app.ProcessId).ProcessName,
                mainWindow = windowTitle,
                launchedByUs = true,
                status = "ready"
            });
        });
    }

    [McpServerTool(Name = "wf_attach_app")]
    [Description("""
        Attach vào một ứng dụng WinForms đang chạy trên máy tính theo Process ID (PID), tên Process, hoặc tiêu đề cửa sổ.
        Nếu tìm thấy nhiều process trùng khớp, sẽ trả về lỗi AMBIGUOUS kèm danh sách PID để người dùng/agent chọn lại.
        """)]
    public static async Task<CallToolResult> AttachApp(
        UiSession session,
        [Description("Mã định danh tiến trình (PID) của ứng dụng cần attach.")]
        int? processId = null,
        [Description("Tên tiến trình (ProcessName, không bao gồm .exe), ví dụ 'SampleApp'.")]
        string? processName = null,
        [Description("Tiêu đề hoặc một phần tiêu đề cửa sổ cần tìm để attach.")]
        string? windowTitle = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            Application? app = null;

            if (processId.HasValue)
            {
                try
                {
                    app = Application.Attach(processId.Value);
                }
                catch (Exception ex)
                {
                    throw new ToolException(ErrorCode.NoSession, $"Không thể attach vào PID {processId.Value}: {ex.Message}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(processName))
            {
                var procs = Process.GetProcessesByName(processName.Replace(".exe", ""));
                if (procs.Length == 0)
                {
                    throw new ToolException(ErrorCode.NoSession, $"Không tìm thấy tiến trình nào có tên '{processName}'.");
                }
                if (procs.Length > 1)
                {
                    var candidates = procs.Select(p => new CandidateDto
                    {
                        Selector = $"processId:{p.Id}",
                        Name = p.MainWindowTitle,
                        AutomationId = p.Id.ToString()
                    }).ToList();

                    throw new ToolException(
                        ErrorCode.Ambiguous,
                        $"Tìm thấy {procs.Length} tiến trình có tên '{processName}'. Hãy chỉ định rõ 'processId'.",
                        candidates: candidates);
                }

                app = Application.Attach(procs[0].Id);
            }
            else if (!string.IsNullOrWhiteSpace(windowTitle))
            {
                var procs = Process.GetProcesses()
                    .Where(p => p.MainWindowTitle.Contains(windowTitle, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (procs.Count == 0)
                {
                    throw new ToolException(ErrorCode.NoSession, $"Không tìm thấy cửa sổ nào chứa tiêu đề '{windowTitle}'.");
                }
                if (procs.Count > 1)
                {
                    var candidates = procs.Select(p => new CandidateDto
                    {
                        Selector = $"processId:{p.Id}",
                        Name = p.MainWindowTitle,
                        AutomationId = p.Id.ToString()
                    }).ToList();

                    throw new ToolException(
                        ErrorCode.Ambiguous,
                        $"Tìm thấy {procs.Count} cửa sổ khớp với '{windowTitle}'. Hãy chỉ định rõ 'processId'.",
                        candidates: candidates);
                }

                app = Application.Attach(procs[0].Id);
            }
            else
            {
                throw new ToolException(ErrorCode.NoSession, "Cần cung cấp ít nhất một trong ba tham số: processId, processName, hoặc windowTitle.");
            }

            session.SetSession(app, launchedByUs: false);

            return await Task.FromResult(McpResults.Ok(new
            {
                processId = app.ProcessId,
                processName = Process.GetProcessById(app.ProcessId).ProcessName,
                attached = true
            }));
        });
    }

    [McpServerTool(Name = "wf_list_windows")]
    [Description("""
        Liệt kê toàn bộ các cửa sổ cấp cao nhất (Top-Level Windows) và các Modal Dialog của ứng dụng đang trong session.
        Giúp AI Agent nắm được các Form đang mở, tiêu đề, trạng thái IsModal và Handle.
        """)]
    public static async Task<CallToolResult> ListWindows(
        UiSession session,
        [Description("Bao gồm cả các cửa sổ con/modal phụ.")]
        bool includeChildren = false,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            session.EnsureAlive();

            var windowsList = await session.RunAsync(() =>
            {
                var topWindows = session.App!.GetAllTopLevelWindows(session.Automation);
                var list = new List<object>();

                foreach (var win in topWindows)
                {
                    bool isModal = win.IsModal || (win.Patterns.Window.IsSupported && win.Patterns.Window.Pattern.IsModal.Value);
                    list.Add(new
                    {
                        title = win.Title,
                        name = win.Name,
                        automationId = win.AutomationId,
                        className = win.ClassName,
                        isModal = isModal,
                        nativeHandle = win.Properties.NativeWindowHandle.ValueOrDefault.ToInt64(),
                        bounds = new[] { (int)win.BoundingRectangle.X, (int)win.BoundingRectangle.Y, (int)win.BoundingRectangle.Width, (int)win.BoundingRectangle.Height },
                        modalCount = win.ModalWindows.Length
                    });

                    if (includeChildren && win.ModalWindows.Length > 0)
                    {
                        foreach (var modal in win.ModalWindows)
                        {
                            list.Add(new
                            {
                                title = modal.Title,
                                name = modal.SafeName(),
                                automationId = modal.SafeAutomationId(),
                                isModal = true,
                                isChildModal = true,
                                parent = win.Title
                            });
                        }
                    }
                }

                return list;
            }, TimeSpan.FromSeconds(10), ct);

            return McpResults.Ok(new
            {
                totalWindows = windowsList.Count,
                windows = windowsList
            });
        });
    }

    [McpServerTool(Name = "wf_close_app")]
    [Description("""
        Đóng ứng dụng đang kiểm thử trong session hiện tại.
        Thực hiện gọi Close() trước, nếu sau timeoutMs vẫn chưa thoát và force=true thì sẽ Kill process.
        Luôn luôn giải phóng tài nguyên session.
        """)]
    public static async Task<CallToolResult> CloseApp(
        UiSession session,
        [Description("Ép buộc tắt (Kill) ngay lập tức nếu app không phản hồi.")]
        bool force = false,
        [Description("Thời gian chờ (ms) trước khi ép tắt. Mặc định 5000ms.")]
        int timeoutMs = 5000,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            if (session.App == null)
            {
                return McpResults.Ok(new { message = "Không có session ứng dụng nào đang mở." });
            }

            var pid = session.ProcessId;

            await session.RunAsync(() =>
            {
                try
                {
                    var proc = Process.GetProcessById(session.App.ProcessId);
                    session.App.Close();
                    var exited = proc.WaitForExit(timeoutMs);
                    if (!exited && force && !session.App.HasExited)
                    {
                        session.App.Kill();
                    }
                }
                catch
                {
                    if (force && session.App != null && !session.App.HasExited)
                    {
                        try { session.App.Kill(); } catch { /* ignore */ }
                    }
                }
                finally
                {
                    session.ResetSession();
                }

                return true;
            }, TimeSpan.FromMilliseconds(timeoutMs + 3000), ct);

            return McpResults.Ok(new
            {
                message = $"Đã đóng ứng dụng (PID: {pid}). Session đã được giải phóng.",
                closedPid = pid
            });
        });
    }
}
