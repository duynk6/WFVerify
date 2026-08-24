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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);
    private const uint GW_CHILD = 5;
    private const uint GW_HWNDNEXT = 2;

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
    /// Cửa sổ cấp cao nhất thuộc đúng một process.
    /// Dùng thay cho <c>Application.GetAllTopLevelWindows(automation)</c> của FlaUI: hàm đó duyệt
    /// con của desktop qua UIA, tức chạm vào cửa sổ của MỌI ứng dụng đang chạy. Chỉ cần một cửa sổ
    /// treo (app UWP bị suspend, ứng dụng không phản hồi) là lời gọi đứng hàng chục giây — đã đo
    /// được 60s cho một lần <c>FindAllChildren()</c> trên desktop. EnumWindows + lọc theo PID chỉ
    /// đọc dữ liệu cửa sổ, không gửi message chờ phản hồi sang process khác.
    /// </summary>
    public static List<TopLevelWindow> GetProcessWindows(int processId, bool visibleOnly = true)
    {
        var result = new List<TopLevelWindow>();

        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var pid);
            if ((int)pid != processId)
            {
                return true;
            }

            if (visibleOnly && !IsWindowVisible(hwnd))
            {
                return true;
            }

            var sbTitle = new StringBuilder(512);
            GetWindowText(hwnd, sbTitle, sbTitle.Capacity);

            var sbClass = new StringBuilder(256);
            GetClassName(hwnd, sbClass, sbClass.Capacity);

            result.Add(new TopLevelWindow(hwnd, processId, sbTitle.ToString(), sbClass.ToString()));
            return true;
        }, IntPtr.Zero);

        return result;
    }

    /// <summary>
    /// Các form MDI child bên trong một cửa sổ cấp cao nhất.
    /// Form MDI child (frmChuanBiSX...) KHÔNG phải cửa sổ cấp cao nhất nên EnumWindows không thấy;
    /// chúng là con TRỰC TIẾP của MDIClient bên trong form cha. Duyệt bằng GetWindow (con trực tiếp)
    /// chứ không dùng EnumChildWindows — hàm đó đệ quy toàn bộ cây con nên trả về cả Button/Label.
    /// </summary>
    public static List<TopLevelWindow> GetMdiChildWindows(IntPtr parent, int processId)
    {
        var result = new List<TopLevelWindow>();

        foreach (var child in ImmediateChildren(parent))
        {
            if (ClassNameOf(child) != "MDIClient") continue;

            foreach (var mdiChild in ImmediateChildren(child))
            {
                if (!IsWindowVisible(mdiChild)) continue;

                var sbTitle = new StringBuilder(512);
                GetWindowText(mdiChild, sbTitle, sbTitle.Capacity);

                result.Add(new TopLevelWindow(mdiChild, processId, sbTitle.ToString(), ClassNameOf(mdiChild)));
            }
        }

        return result;
    }

    private static IEnumerable<IntPtr> ImmediateChildren(IntPtr parent)
    {
        var child = GetWindow(parent, GW_CHILD);
        while (child != IntPtr.Zero)
        {
            yield return child;
            child = GetWindow(child, GW_HWNDNEXT);
        }
    }

    private static string ClassNameOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
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
