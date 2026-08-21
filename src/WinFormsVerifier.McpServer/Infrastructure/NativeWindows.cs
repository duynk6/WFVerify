using System.Runtime.InteropServices;
using System.Text;

namespace WinFormsVerifier.Infrastructure;

/// <summary>
/// Liệt kê cửa sổ cấp cao nhất bằng Win32.
/// Dùng thay cho <c>Process.MainWindowTitle</c> vì MainWindowTitle chỉ trả về tiêu đề của
/// MỘT cửa sổ chính: ứng dụng đang đứng ở form đăng nhập, splash screen, hoặc dialog chọn
/// cơ sở dữ liệu thường có MainWindowTitle rỗng hoặc khác hẳn tiêu đề đang hiển thị.
/// </summary>
public static class NativeWindows
{
    public sealed record TopLevelWindow(IntPtr Hwnd, int ProcessId, string Title, string ClassName);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    public static List<TopLevelWindow> GetVisibleTopLevelWindows()
    {
        var result = new List<TopLevelWindow>();

        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }

            var sbTitle = new StringBuilder(512);
            GetWindowText(hwnd, sbTitle, sbTitle.Capacity);
            var title = sbTitle.ToString();

            var sbClass = new StringBuilder(256);
            GetClassName(hwnd, sbClass, sbClass.Capacity);

            GetWindowThreadProcessId(hwnd, out var pid);

            result.Add(new TopLevelWindow(hwnd, (int)pid, title, sbClass.ToString()));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>
    /// Các process có cửa sổ hiển thị mà tiêu đề chứa <paramref name="titlePart"/>.
    /// Trả về map processId -> tiêu đề khớp đầu tiên.
    /// </summary>
    public static Dictionary<int, string> FindProcessesByWindowTitle(string titlePart)
    {
        var matches = new Dictionary<int, string>();

        foreach (var win in GetVisibleTopLevelWindows())
        {
            if (string.IsNullOrWhiteSpace(win.Title)) continue;
            if (win.ProcessId <= 4) continue; // System / Idle
            if (!win.Title.Contains(titlePart, StringComparison.OrdinalIgnoreCase)) continue;

            if (!matches.ContainsKey(win.ProcessId))
            {
                matches[win.ProcessId] = win.Title;
            }
        }

        return matches;
    }
}
