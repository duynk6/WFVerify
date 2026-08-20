using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.Extensions.Logging;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Services;

public sealed class UiSession : IDisposable
{
    private readonly UiThread _uiThread;
    private readonly ILogger<UiSession> _logger;
    private readonly object _lock = new();

    public Application? App { get; private set; }
    public UIA3Automation Automation { get; }
    public Window? CachedMainWindow { get; private set; }
    public bool LaunchedByUs { get; private set; }
    public int? ProcessId => App?.ProcessId;
    public string? ProcessName { get; private set; }

    public UiSession(UiThread uiThread, ILogger<UiSession> logger)
    {
        _uiThread = uiThread;
        _logger = logger;
        Automation = new UIA3Automation();
    }

    public async Task<T> RunAsync<T>(Func<T> work, TimeSpan timeout, CancellationToken ct = default)
    {
        return await _uiThread.RunAsync(work, timeout, ct);
    }

    public void EnsureAlive()
    {
        if (App == null)
        {
            throw new ToolException(
                ErrorCode.NoSession,
                "Chưa có session nào hoạt động. Hãy gọi 'wf_launch_app' hoặc 'wf_attach_app' trước.",
                "Sử dụng wf_launch_app với đường dẫn exe hoặc wf_attach_app với processId/windowTitle.");
        }

        try
        {
            if (App.HasExited)
            {
                var pid = App.ProcessId;
                ResetSession();
                throw new ToolException(
                    ErrorCode.AppExited,
                    $"Ứng dụng (PID: {pid}) đã kết thúc.",
                    "Khởi động lại ứng dụng bằng wf_launch_app hoặc attach vào process mới.");
            }
        }
        catch (InvalidOperationException)
        {
            ResetSession();
            throw new ToolException(ErrorCode.AppExited, "Không thể truy cập process đích (đã thoát).");
        }
    }

    public void SetSession(Application app, bool launchedByUs, string? processName = null)
    {
        lock (_lock)
        {
            if (App != null && LaunchedByUs && !App.HasExited)
            {
                try { App.Kill(); } catch { /* ignore */ }
            }

            App = app;
            LaunchedByUs = launchedByUs;
            ProcessName = processName ?? (app.HasExited ? null : Process.GetProcessById(app.ProcessId).ProcessName);
            CachedMainWindow = null;
        }
    }

    public Window ResolveWindow(string? selector = null)
    {
        EnsureAlive();

        // 1. If selector is provided, look for matching window
        if (!string.IsNullOrWhiteSpace(selector))
        {
            var windows = App!.GetAllTopLevelWindows(Automation);
            var matched = FindWindowBySelector(windows, selector);
            if (matched != null)
            {
                return matched;
            }

            throw new ToolException(
                ErrorCode.WindowNotFound,
                $"Không tìm thấy cửa sổ nào khớp với selector '{selector}'.",
                "Gọi 'wf_list_windows' để xem danh sách tất cả các cửa sổ đang mở.");
        }

        // 2. If selector is null, check for any active modal dialog first!
        var topWindows = App!.GetAllTopLevelWindows(Automation);
        var modal = topWindows.FirstOrDefault(w => w.IsModal || w.ModalWindows.Length > 0);
        if (modal != null)
        {
            if (modal.ModalWindows.Length > 0)
            {
                return modal.ModalWindows.Last();
            }
            return modal;
        }

        // 3. Fallback to main window
        try
        {
            var main = App.GetMainWindow(Automation);
            if (main != null)
            {
                CachedMainWindow = main;
                return main;
            }
        }
        catch
        {
            // Fallback to first available top-level window
        }

        var first = topWindows.FirstOrDefault();
        if (first != null)
        {
            CachedMainWindow = first;
            return first;
        }

        throw new ToolException(
            ErrorCode.WindowNotFound,
            "Không tìm thấy cửa sổ nào của ứng dụng đang chạy.",
            "Hãy kiểm tra lại xem ứng dụng đã hiển thị giao diện chưa hoặc dùng wf_list_windows.");
    }

    private static Window? FindWindowBySelector(IEnumerable<Window> windows, string selector)
    {
        var parts = selector.Split(':', 2);
        var prefix = parts.Length == 2 ? parts[0].Trim().ToLowerInvariant() : "name";
        var value = parts.Length == 2 ? parts[1].Trim() : selector.Trim();

        foreach (var win in windows)
        {
            if (prefix switch
            {
                "id" => string.Equals(win.AutomationId, value, StringComparison.OrdinalIgnoreCase),
                "name" => string.Equals(win.Name, value, StringComparison.OrdinalIgnoreCase),
                "name~" => win.Name?.Contains(value, StringComparison.OrdinalIgnoreCase) == true,
                "class" => string.Equals(win.ClassName, value, StringComparison.OrdinalIgnoreCase),
                "title" => string.Equals(win.Title, value, StringComparison.OrdinalIgnoreCase),
                "title~" => win.Title?.Contains(value, StringComparison.OrdinalIgnoreCase) == true,
                _ => win.Name?.Contains(selector, StringComparison.OrdinalIgnoreCase) == true ||
                     win.Title?.Contains(selector, StringComparison.OrdinalIgnoreCase) == true
            })
            {
                return win;
            }
        }

        return null;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

    public (bool HasModal, IntPtr DialogHandle, Window? ModalWindow, string? Title, string? Text, List<string>? Buttons) DetectBlockingModal(Window? currentWindow = null)
    {
        if (App == null || App.HasExited) return (false, IntPtr.Zero, null, null, null, null);

        var pid = (uint)App.ProcessId;
        var dialogHandles = new List<IntPtr>();

        EnumWindows((hwnd, _) =>
        {
            if (IsWindowVisible(hwnd))
            {
                GetWindowThreadProcessId(hwnd, out var winPid);
                if (winPid == pid)
                {
                    var sbClass = new System.Text.StringBuilder(256);
                    GetClassName(hwnd, sbClass, 256);
                    var className = sbClass.ToString();

                    if (className == "#32770")
                    {
                        dialogHandles.Add(hwnd);
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        if (dialogHandles.Count > 0)
        {
            var dialogHwnd = dialogHandles.Last();
            var currentHandle = currentWindow?.Properties.NativeWindowHandle.ValueOrDefault ?? IntPtr.Zero;

            if (currentWindow == null || dialogHwnd != currentHandle)
            {
                var sbTitle = new System.Text.StringBuilder(256);
                GetWindowText(dialogHwnd, sbTitle, 256);
                var title = sbTitle.ToString();

                var (text, buttons) = ExtractDialogDetailsFast(dialogHwnd);

                return (true, dialogHwnd, null, title, text, buttons);
            }
        }

        return (false, IntPtr.Zero, null, null, null, null);
    }

    private static (string Text, List<string> Buttons) ExtractDialogDetailsFast(IntPtr dialogHwnd)
    {
        var texts = new List<string>();
        var buttons = new List<string>();

        EnumChildWindows(dialogHwnd, (childHwnd, _) =>
        {
            if (IsWindowVisible(childHwnd))
            {
                var sbClass = new System.Text.StringBuilder(256);
                GetClassName(childHwnd, sbClass, 256);
                var className = sbClass.ToString();

                var sbText = new System.Text.StringBuilder(1024);
                GetWindowText(childHwnd, sbText, 1024);
                var text = sbText.ToString().Trim();

                if (!string.IsNullOrEmpty(text))
                {
                    if (className == "Static")
                    {
                        texts.Add(text);
                    }
                    else if (className == "Button")
                    {
                        buttons.Add(text);
                    }
                }
            }
            return true;
        }, IntPtr.Zero);

        return (string.Join(" ", texts), buttons.Count > 0 ? buttons : new List<string> { "OK" });
    }

    public void ResetSession()
    {
        lock (_lock)
        {
            App = null;
            CachedMainWindow = null;
            LaunchedByUs = false;
            ProcessName = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (App != null && LaunchedByUs && !App.HasExited)
            {
                try
                {
                    _logger.LogInformation("Đang đóng ứng dụng PID: {Pid} do server khởi chạy...", App.ProcessId);
                    App.Close();
                    if (!App.HasExited)
                    {
                        App.Kill();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi khi dọn dẹp ứng dụng lúc đóng server");
                }
            }

            ResetSession();

            try
            {
                Automation.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi khi dispose UIA3Automation");
            }
        }
    }
}
