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
        [Description("Biến môi trường truyền cho tiến trình, mỗi phần tử dạng 'TEN=GIA_TRI' (vd 'ConnectionStrings__Main=Server=.;Database=UAT'). Dùng để đổi chuỗi kết nối SQL hoặc môi trường mà không phải sửa file config.")]
        string[]? environment = null,
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

            if (environment != null && environment.Length > 0)
            {
                foreach (var entry in environment)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;

                    var sep = entry.IndexOf('=');
                    if (sep <= 0)
                    {
                        throw new ToolException(
                            ErrorCode.Internal,
                            $"Biến môi trường '{entry}' không đúng định dạng.",
                            "Mỗi phần tử phải có dạng 'TEN=GIA_TRI', ví dụ 'DB_ENV=UAT'.");
                    }

                    psi.Environment[entry[..sep].Trim()] = entry[(sep + 1)..];
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
        [Description("Tiêu đề hoặc một phần tiêu đề cửa sổ cần tìm để attach. So khớp với MỌI cửa sổ đang hiển thị, nên vẫn tìm được khi ứng dụng đang đứng ở form đăng nhập hoặc dialog chọn cơ sở dữ liệu.")]
        string? windowTitle = null,
        [Description("Thời gian tối đa (ms) chờ tiến trình/cửa sổ xuất hiện trước khi báo lỗi. Mặc định 0 (không chờ). Đặt vài nghìn ms nếu ứng dụng đang khởi động.")]
        int waitForWindowMs = 0,
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
                var wanted = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                var procs = await WaitForAsync(
                    () =>
                    {
                        var found = Process.GetProcessesByName(wanted);
                        return found.Length > 0 ? found : null;
                    },
                    waitForWindowMs, ct) ?? Array.Empty<Process>();

                if (procs.Length == 0)
                {
                    throw new ToolException(
                        ErrorCode.NoSession,
                        $"Không tìm thấy tiến trình nào có tên '{processName}'" + (waitForWindowMs > 0 ? $" sau {waitForWindowMs}ms chờ." : "."),
                        "Kiểm tra ứng dụng đã chạy chưa, hoặc tăng 'waitForWindowMs' nếu nó đang khởi động.");
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
                // Quét MỌI cửa sổ hiển thị chứ không chỉ MainWindowTitle: app đang ở form
                // đăng nhập / splash / dialog chọn DB thường có MainWindowTitle rỗng.
                var matches = await WaitForAsync(
                    () =>
                    {
                        var found = NativeWindows.FindProcessesByWindowTitle(windowTitle);
                        return found.Count > 0 ? found : null;
                    },
                    waitForWindowMs, ct) ?? new Dictionary<int, string>();

                if (matches.Count == 0)
                {
                    throw new ToolException(
                        ErrorCode.NoSession,
                        $"Không tìm thấy cửa sổ nào chứa tiêu đề '{windowTitle}'" + (waitForWindowMs > 0 ? $" sau {waitForWindowMs}ms chờ." : "."),
                        "Kiểm tra lại tiêu đề, hoặc tăng 'waitForWindowMs' nếu ứng dụng đang khởi động.");
                }
                if (matches.Count > 1)
                {
                    var candidates = matches.Select(kv => new CandidateDto
                    {
                        Selector = $"processId:{kv.Key}",
                        Name = kv.Value,
                        AutomationId = kv.Key.ToString()
                    }).ToList();

                    throw new ToolException(
                        ErrorCode.Ambiguous,
                        $"Tìm thấy {matches.Count} tiến trình có cửa sổ khớp với '{windowTitle}'. Hãy chỉ định rõ 'processId'.",
                        candidates: candidates);
                }

                app = Application.Attach(matches.Keys.First());
            }
            else
            {
                throw new ToolException(ErrorCode.NoSession, "Cần cung cấp ít nhất một trong ba tham số: processId, processName, hoặc windowTitle.");
            }

            session.SetSession(app, launchedByUs: false);

            var pid = app.ProcessId;
            var visibleWindows = NativeWindows.GetVisibleTopLevelWindows()
                .Where(w => w.ProcessId == pid && !string.IsNullOrWhiteSpace(w.Title))
                .Select(w => w.Title)
                .Distinct()
                .ToList();

            return await Task.FromResult(McpResults.Ok(new
            {
                processId = pid,
                processName = Process.GetProcessById(pid).ProcessName,
                attached = true,
                launchedByUs = false,
                windows = visibleWindows,
                note = "Tiến trình này KHÔNG do server khởi chạy nên sẽ không bị đóng khi kết thúc session. Dùng wf_detach_app để rời session mà vẫn giữ ứng dụng chạy."
            }));
        });
    }

    /// <summary>Chờ cho tới khi probe trả về khác null, hoặc hết thời gian.</summary>
    private static async Task<T?> WaitForAsync<T>(Func<T?> probe, int timeoutMs, CancellationToken ct) where T : class
    {
        var first = probe();
        if (first != null || timeoutMs <= 0)
        {
            return first;
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(250, ct);
            var found = probe();
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    [McpServerTool(Name = "wf_detach_app")]
    [Description(
        "Rời khỏi session hiện tại mà KHÔNG đóng ứng dụng — tiến trình vẫn tiếp tục chạy. " +
        "Dùng tool này khi làm việc xong với một ứng dụng được attach bằng wf_attach_app " +
        "(ví dụ app đã được đăng nhập / chọn cơ sở dữ liệu thủ công), thay cho wf_close_app.")]
    public static async Task<CallToolResult> DetachApp(
        UiSession session,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            if (session.App == null)
            {
                return await Task.FromResult(McpResults.Ok(new { message = "Không có session nào đang mở." }));
            }

            var pid = session.ProcessId;
            var launchedByUs = session.LaunchedByUs;
            session.ResetSession();

            var warnings = launchedByUs
                ? new[] { $"Tiến trình {pid} do server khởi chạy nhưng nay đã tách khỏi session, nên sẽ KHÔNG được tự dọn dẹp khi server tắt. Hãy tự đóng ứng dụng khi xong." }
                : null;

            return await Task.FromResult(McpResults.Ok(new
            {
                message = $"Đã tách khỏi session (PID: {pid}). Ứng dụng vẫn đang chạy.",
                detachedPid = pid
            }, warnings));
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

            // AGENTS.md Rule 6: chỉ được kết thúc tiến trình do server khởi chạy.
            // Ứng dụng do người dùng tự mở thường đã đăng nhập / chọn CSDL bằng tay
            // — đóng nó đi là phá mất phiên làm việc không dựng lại được.
            if (!session.LaunchedByUs)
            {
                throw new ToolException(
                    ErrorCode.PathDenied,
                    $"Tiến trình {session.ProcessId} không do server khởi chạy (attach vào) nên không được phép đóng.",
                    "Dùng wf_detach_app để rời session mà vẫn giữ ứng dụng chạy. Nếu thật sự cần đóng, hãy tự đóng ứng dụng đó.",
                    details: new { processId = session.ProcessId, launchedByUs = false });
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
