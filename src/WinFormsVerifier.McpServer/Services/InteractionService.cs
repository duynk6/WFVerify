using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Tools;
using FlaUI.Core.WindowsAPI;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Services;

public class InteractionResult
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
    public object? Data { get; set; }
}

public sealed class InteractionService
{
    private readonly UiSession _session;

    public InteractionService(UiSession session)
    {
        _session = session;
    }

    public InteractionResult Invoke(AutomationElement element)
    {
        CheckModalBlock();

        var success = false;
        var p = element.Patterns;

        // THỨ TỰ CỐ Ý: input phi chặn (PostMessage / mouse_event) TRƯỚC, pattern SAU.
        // Lý do: UIA InvokePattern.Invoke() chạy ĐỒNG BỘ — nếu event handler của control
        // gọi MessageBox.Show(), Invoke() không trả về cho tới khi dialog bị đóng, khiến
        // luồng STA của server treo tới khi timeout (và poison detection báo session hỏng).
        // Đã kiểm chứng bằng LiveUiWorkflowTests: đảo lại thành pattern-first làm test
        // timeout 5s ngay tại bước click btnLogin (handler mở MessageBox).
        // => KHÔNG đảo thứ tự này để "khớp" .agents/rules/ui-automation-rules.md.
        try
        {
            var hwnd = element.SafeNativeWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                PostMessage(hwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                success = true;
            }
            else
            {
                var rect = element.SafeBoundingRectangle();
                if (!rect.IsEmpty && rect.Width > 0 && rect.Height > 0)
                {
                    var x = rect.Left + rect.Width / 2;
                    var y = rect.Top + rect.Height / 2;
                    SetCursorPos(x, y);
                    Thread.Sleep(20);
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    success = true;
                }
            }
        }
        catch { /* fallback */ }

        if (!success && p.Invoke.IsSupported)
        {
            try
            {
                p.Invoke.Pattern.Invoke();
                success = true;
            }
            catch { /* fallback */ }
        }

        if (!success && p.SelectionItem.IsSupported)
        {
            try
            {
                p.SelectionItem.Pattern.Select();
                success = true;
            }
            catch { /* fallback */ }
        }

        if (!success && p.LegacyIAccessible.IsSupported)
        {
            try
            {
                p.LegacyIAccessible.Pattern.DoDefaultAction();
                success = true;
            }
            catch { /* fallback */ }
        }

        if (!success && p.Toggle.IsSupported)
        {
            try
            {
                p.Toggle.Pattern.Toggle();
                success = true;
            }
            catch { /* fallback */ }
        }

        if (!success)
        {
            var patterns = ElementDto.DetectSupportedPatterns(element);
            throw new ToolException(
                ErrorCode.PatternUnsupported,
                $"Control '{element.SafeLabel()}' ({element.SafeControlTypeName()}) không hỗ trợ thao tác Invoke.",
                $"Các pattern khả dụng: [{string.Join(", ", patterns)}].",
                details: new { Patterns = patterns });
        }

        return PostAction($"Đã click/kích hoạt '{element.SafeLabel()}'.");
    }

    public InteractionResult SetValue(AutomationElement element, string value, string mode = "replace", bool verify = true)
    {
        CheckModalBlock();

        var isPassword = element.SafeIsPassword();

        var targetValue = value;
        if (mode.Equals("append", StringComparison.OrdinalIgnoreCase))
        {
            var current = GetElementValue(element);
            targetValue = current + value;
        }

        var setSuccess = false;

        // Ô mật khẩu vẫn dùng ValuePattern: đã kiểm chứng trên WinForms TextBox có
        // PasswordChar thì SetValue có tác dụng (chỉ ĐỌC lại mới bị chặn -> "Access denied").
        // Điểm cần canh là đường bàn phím phía dưới, xem FocusForTyping.
        if (element.Patterns.Value.IsSupported && !element.Patterns.Value.Pattern.IsReadOnly.Value)
        {
            try
            {
                element.Patterns.Value.Pattern.SetValue(targetValue);
                setSuccess = true;
            }
            catch { /* fallback to keyboard */ }
        }

        if (!setSuccess)
        {
            try
            {
                FocusForTyping(element);

                if (mode.Equals("replace", StringComparison.OrdinalIgnoreCase))
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                    Keyboard.Type(VirtualKeyShort.BACK);
                }
                Keyboard.Type(targetValue);
                setSuccess = true;
            }
            catch (ToolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ToolException(
                    ErrorCode.PatternUnsupported,
                    $"Không thể nhập giá trị vào control '{element.SafeLabel()}': {ex.Message}",
                    "Hãy kiểm tra xem control có bị Read-Only hoặc disabled không.");
            }
        }

        var shownValue = isPassword ? new string('*', targetValue.Length) : targetValue;
        var res = PostAction($"Đã đặt giá trị cho '{element.SafeLabel()}' thành '{shownValue}'.");

        if (verify)
        {
            if (isPassword)
            {
                // Đọc lại ô mật khẩu luôn trả "Access denied" — so sánh sẽ luôn sai, đừng báo nhầm.
                res.Warnings.Add(
                    "Đây là ô mật khẩu nên không thể đọc lại để xác thực (UIA chặn). " +
                    "Giá trị đã được gõ bằng bàn phím; hãy kiểm tra kết quả qua hành vi của ứng dụng.");
            }
            else
            {
                var readBack = GetElementValue(element);
                if (!string.Equals(readBack, targetValue, StringComparison.Ordinal))
                {
                    res.Warnings.Add($"Giá trị đọc lại sau khi set ('{readBack}') không khớp hoàn toàn với giá trị đã đặt ('{targetValue}').");
                }
            }
        }

        return res;
    }

    /// <summary>
    /// Đảm bảo control thật sự nhận keyboard focus trước khi gõ. Nếu cửa sổ không ở
    /// foreground thì Keyboard.Type() sẽ gõ vào ứng dụng khác (hoặc rơi vào hư không) mà
    /// không hề báo lỗi — tool sẽ báo "đã nhập" trong khi ô vẫn rỗng.
    /// </summary>
    private static void FocusForTyping(AutomationElement element)
    {
        var root = element.SafeNativeWindowHandle();
        if (root != IntPtr.Zero)
        {
            root = GetAncestor(root, GA_ROOT);
            if (root != IntPtr.Zero)
            {
                SetForegroundWindow(root);
                Thread.Sleep(50);
            }
        }

        try { element.Focus(); } catch { /* thử tiếp bằng FocusNative */ }
        Thread.Sleep(50);

        if (!element.SafeHasKeyboardFocus())
        {
            try { element.FocusNative(); } catch { /* ignore */ }
            Thread.Sleep(80);
        }

        if (!element.SafeHasKeyboardFocus())
        {
            throw new ToolException(
                ErrorCode.Internal,
                $"Không đưa được keyboard focus vào '{element.SafeLabel()}' nên không thể nhập bằng bàn phím.",
                "Cửa sổ ứng dụng có thể đang bị che hoặc minimize. Gọi wf_focus vào cửa sổ trước, " +
                "hoặc đảm bảo màn hình không bị khoá.");
        }
    }

    public InteractionResult Toggle(AutomationElement element, string state = "toggle")
    {
        CheckModalBlock();

        if (element.Patterns.Toggle.IsSupported)
        {
            var current = element.Patterns.Toggle.Pattern.ToggleState.Value;
            var target = state.ToLowerInvariant();

            if (target == "on" && current != ToggleState.On)
            {
                element.Patterns.Toggle.Pattern.Toggle();
            }
            else if (target == "off" && current != ToggleState.Off)
            {
                element.Patterns.Toggle.Pattern.Toggle();
            }
            else if (target == "toggle")
            {
                element.Patterns.Toggle.Pattern.Toggle();
            }

            return PostAction($"Đã chuyển trạng thái toggle của '{element.SafeLabel()}' sang '{element.Patterns.Toggle.Pattern.ToggleState.Value}'.");
        }

        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return PostAction($"Đã chọn '{element.SafeLabel()}'.");
        }

        return Invoke(element);
    }

    public InteractionResult Select(AutomationElement element, string? item = null, int? index = null)
    {
        CheckModalBlock();

        // If this element itself is a selectable item
        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return PostAction($"Đã chọn mục '{element.SafeLabel()}'.");
        }

        // If it is a container (ComboBox / ListBox / TabControl)
        if (element.Patterns.ExpandCollapse.IsSupported && element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
        {
            try
            {
                element.Patterns.ExpandCollapse.Pattern.Expand();
                Thread.Sleep(100);
            }
            catch { /* continue */ }
        }

        var children = element.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem).Or(cf.ByControlType(ControlType.TabItem)));

        if (index.HasValue && index.Value >= 0 && index.Value < children.Length)
        {
            var target = children[index.Value];
            if (target.Patterns.SelectionItem.IsSupported)
            {
                target.Patterns.SelectionItem.Pattern.Select();
            }
            else
            {
                target.Click();
            }
            return PostAction($"Đã chọn index {index.Value} ('{target.SafeLabel()}') trong '{element.SafeLabel()}'.");
        }

        if (!string.IsNullOrEmpty(item))
        {
            var matched = children.FirstOrDefault(c => c.SafeName().Contains(item, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
            {
                if (matched.Patterns.SelectionItem.IsSupported)
                {
                    matched.Patterns.SelectionItem.Pattern.Select();
                }
                else
                {
                    matched.Click();
                }
                return PostAction($"Đã chọn mục '{matched.SafeLabel()}' trong '{element.SafeLabel()}'.");
            }

            throw new ToolException(
                ErrorCode.ElementNotFound,
                $"Không tìm thấy mục '{item}' trong danh sách '{element.SafeLabel()}'.",
                $"Danh sách các mục có sẵn: [{string.Join(", ", children.Select(c => c.SafeLabel()))}]");
        }

        throw new ToolException(ErrorCode.Internal, "Cần cung cấp ít nhất tham số 'item' hoặc 'index' để select.");
    }

    public InteractionResult Expand(AutomationElement element, bool expand = true)
    {
        CheckModalBlock();

        if (element.Patterns.ExpandCollapse.IsSupported)
        {
            if (expand)
            {
                element.Patterns.ExpandCollapse.Pattern.Expand();
            }
            else
            {
                element.Patterns.ExpandCollapse.Pattern.Collapse();
            }
            return PostAction($"Đã {(expand ? "mở rộng" : "thu gọn")} '{element.SafeLabel()}'.");
        }

        throw new ToolException(ErrorCode.PatternUnsupported, $"Control '{element.SafeLabel()}' không hỗ trợ ExpandCollapse.");
    }

    public InteractionResult Focus(AutomationElement element)
    {
        element.Focus();
        return PostAction($"Đã focus vào '{element.SafeLabel()}'.");
    }

    public InteractionResult ScrollIntoView(AutomationElement element)
    {
        if (element.Patterns.ScrollItem.IsSupported)
        {
            element.Patterns.ScrollItem.Pattern.ScrollIntoView();
            return PostAction($"Đã cuộn tới '{element.SafeLabel()}'.");
        }

        throw new ToolException(ErrorCode.PatternUnsupported, $"Control '{element.SafeLabel()}' không hỗ trợ ScrollItem.");
    }

    public InteractionResult SendKeys(string keys, AutomationElement? target = null)
    {
        CheckModalBlock();

        if (target != null)
        {
            target.Focus();
            Thread.Sleep(50);
        }

        Keyboard.Type(keys);
        return PostAction($"Đã gửi phím '{keys}'.");
    }

    public InteractionResult GridRead(AutomationElement element, int startRow = 0, int maxRows = 50, int maxCols = 20)
    {
        var sb = new StringBuilder();
        int rowCount = 0;
        int colCount = 0;

        try
        {
            if (element.Patterns.Grid.IsSupported)
            {
                rowCount = element.Patterns.Grid.Pattern.RowCount.ValueOrDefault;
                colCount = element.Patterns.Grid.Pattern.ColumnCount.ValueOrDefault;
            }
        }
        catch { /* ignore */ }

        var dgv = element.AsDataGridView();
        var dgvRows = dgv?.Rows;
        if (rowCount == 0 && dgvRows != null)
        {
            rowCount = dgvRows.Length;
        }

        int endRow = Math.Min(rowCount > 0 ? rowCount : (dgvRows?.Length ?? 0), startRow + maxRows);
        int colsToRead = Math.Min(colCount > 0 ? colCount : 20, maxCols);

        sb.AppendLine($"Bảng: {rowCount} dòng × {colCount} cột (Hiển thị từ dòng {startRow} đến {Math.Max(0, endRow - 1)}):");
        sb.AppendLine(new string('-', 60));

        for (int r = startRow; r < endRow; r++)
        {
            var cellValues = new List<string>();
            for (int c = 0; c < colsToRead; c++)
            {
                string cellText = "";
                var cell = GetGridCellElement(element, r, c);
                if (cell != null)
                {
                    cellText = GetElementValue(cell) ?? cell.SafeName();
                }
                cellValues.Add(cellText.PadRight(15));
            }
            sb.AppendLine($"Row {r:D2} | " + string.Join(" | ", cellValues));
        }

        return new InteractionResult
        {
            Success = true,
            Message = sb.ToString(),
            Data = new { TotalRows = rowCount, TotalCols = colCount, DisplayedRows = endRow - startRow }
        };
    }

    public InteractionResult GridSetCell(AutomationElement element, int row, int col, string value)
    {
        CheckModalBlock();

        var cell = GetGridCellElement(element, row, col);
        if (cell == null)
        {
            throw new ToolException(ErrorCode.ElementNotFound, $"Không tìm thấy ô tại dòng {row}, cột {col}.");
        }

        return SetValue(cell, value);
    }

    private static AutomationElement? GetGridCellElement(AutomationElement gridElement, int row, int col)
    {
        try
        {
            if (gridElement.Patterns.Grid.IsSupported)
            {
                try
                {
                    var item = gridElement.Patterns.Grid.Pattern.GetItem(row, col);
                    if (item != null) return item;
                }
                catch { /* WinForms DataGridView throws NotImplementedException */ }
            }

            var dgv = gridElement.AsDataGridView();
            if (dgv != null && dgv.Rows.Length > row)
            {
                var r = dgv.Rows[row];
                if (r.Cells.Length > col)
                {
                    return r.Cells[col];
                }
            }

            // Fallback: child search
            var rows = gridElement.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem).Or(cf.ByControlType(ControlType.Custom)));
            if (rows.Length > row)
            {
                var cells = rows[row].FindAllChildren();
                if (cells.Length > col)
                {
                    return cells[col];
                }
            }
        }
        catch { }

        return null;
    }

    public InteractionResult MenuClick(Window window, string menuPath)
    {
        CheckModalBlock();

        var segments = menuPath.Split(new[] { '>', '/', '\\' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ToolException(ErrorCode.Internal, "Đường dẫn menu không hợp lệ.");
        }

        AutomationElement currentScope = window;
        for (int i = 0; i < segments.Length; i++)
        {
            var seg = segments[i];
            var isLast = i == segments.Length - 1;

            var menuItem = Retry.WhileNull(
                () => currentScope.FindFirstDescendant(cf => cf.ByControlType(ControlType.MenuItem).And(cf.ByName(seg))),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromMilliseconds(100)).Result;

            if (menuItem == null)
            {
                // Try contains search
                menuItem = currentScope.FindAllDescendants(cf => cf.ByControlType(ControlType.MenuItem))
                    .FirstOrDefault(m => m.SafeName().Contains(seg, StringComparison.OrdinalIgnoreCase));
            }

            if (menuItem == null)
            {
                throw new ToolException(
                    ErrorCode.ElementNotFound,
                    $"Không tìm thấy mục menu '{seg}' trong đường dẫn '{menuPath}'.",
                    "Hãy kiểm tra lại tên menu hoặc dùng wf_get_ui_tree.");
            }

            if (isLast)
            {
                return Invoke(menuItem);
            }
            else
            {
                if (menuItem.Patterns.ExpandCollapse.IsSupported &&
                    menuItem.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
                {
                    menuItem.Patterns.ExpandCollapse.Pattern.Expand();
                }
                else
                {
                    menuItem.Click();
                }
                Thread.Sleep(150);
                currentScope = window; // Menu popups often attach to top level
            }
        }

        return PostAction($"Đã click menu '{menuPath}'.");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
    private const uint GA_ROOT = 2;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    private const uint BM_CLICK = 0x00F5;

    public InteractionResult DialogRespond(string buttonName, int waitMs = 2000)
    {
        // Dò lặp thay vì một lần: dialog có thể chưa kịp hiện khi tool được gọi ngay sau
        // một thao tác khác. Nếu hết thời gian chờ mà vẫn không có -> dialog đã đóng từ trước.
        var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, waitMs));
        bool hasModal;
        IntPtr dialogHwnd;
        Window? modalWindow;
        string? title, text;
        List<string>? buttons;

        do
        {
            (hasModal, dialogHwnd, modalWindow, title, text, buttons) = _session.DetectBlockingModal();
            if (hasModal) break;
            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        if (!hasModal)
        {
            throw new ToolException(
                ErrorCode.WindowNotFound,
                $"Không có modal dialog nào đang mở để phản hồi (đã chờ {waitMs}ms).",
                "Dialog có thể đã tự đóng trước khi tool này chạy. Kiểm tra lại 'warnings' của thao tác trước đó, " +
                "hoặc dùng wf_list_windows để xem các cửa sổ đang hoạt động.");
        }

        if (dialogHwnd != IntPtr.Zero)
        {
            IntPtr targetBtnHwnd = IntPtr.Zero;
            string foundName = buttonName;

            EnumChildWindows(dialogHwnd, (childHwnd, _) =>
            {
                var sbClass = new System.Text.StringBuilder(256);
                GetClassName(childHwnd, sbClass, 256);
                if (sbClass.ToString() == "Button")
                {
                    var sbText = new System.Text.StringBuilder(256);
                    GetWindowText(childHwnd, sbText, 256);
                    var btnText = sbText.ToString();
                    if (string.Equals(btnText, buttonName, StringComparison.OrdinalIgnoreCase) ||
                        btnText.Contains(buttonName, StringComparison.OrdinalIgnoreCase))
                    {
                        targetBtnHwnd = childHwnd;
                        foundName = btnText;
                        return false;
                    }
                }
                return true;
            }, IntPtr.Zero);

            if (targetBtnHwnd != IntPtr.Zero)
            {
                SendMessage(targetBtnHwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);

                // BẮT BUỘC xác minh: BM_CLICK không phải lúc nào cũng đóng được dialog.
                // Trước đây tool báo thành công ngay mà không kiểm tra, dẫn tới việc agent
                // tưởng dialog đã đóng trong khi nó vẫn đang chặn ứng dụng.
                if (WaitUntilDialogClosed(dialogHwnd, TimeSpan.FromMilliseconds(1500)))
                {
                    return new InteractionResult
                    {
                        Success = true,
                        Message = $"Đã bấm nút '{foundName}' để đóng modal dialog '{title}'."
                    };
                }

                // Thử lại bằng UIA trên chính nút đó trước khi bỏ cuộc.
                try
                {
                    var btnElement = _session.Automation.FromHandle(targetBtnHwnd);
                    if (btnElement?.Patterns.Invoke.IsSupported == true)
                    {
                        btnElement.Patterns.Invoke.Pattern.Invoke();
                    }
                }
                catch { /* rơi xuống kiểm tra bên dưới */ }

                if (WaitUntilDialogClosed(dialogHwnd, TimeSpan.FromMilliseconds(1500)))
                {
                    return new InteractionResult
                    {
                        Success = true,
                        Message = $"Đã bấm nút '{foundName}' để đóng modal dialog '{title}' (qua InvokePattern sau khi BM_CLICK không ăn)."
                    };
                }

                throw new ToolException(
                    ErrorCode.Internal,
                    $"Đã bấm nút '{foundName}' nhưng modal dialog '{title}' vẫn đang mở.",
                    "Dialog có thể đang chờ thao tác khác, hoặc nút này không phải nút đóng. " +
                    "Thử lại với tên nút khác, hoặc dùng wf_get_ui_tree để xem các nút hiện có trên dialog.",
                    details: new { DialogTitle = title, ClickedButton = foundName, AvailableButtons = buttons });
            }
        }

        if (modalWindow != null)
        {
            var btn = modalWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(buttonName)));
            if (btn == null)
            {
                btn = modalWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(b => b.SafeName().Contains(buttonName, StringComparison.OrdinalIgnoreCase));
            }

            if (btn != null)
            {
                btn.Click();
                if (!WaitUntilNoModal(TimeSpan.FromMilliseconds(1500)))
                {
                    throw new ToolException(
                        ErrorCode.Internal,
                        $"Đã bấm nút '{btn.SafeLabel()}' nhưng modal dialog '{title}' vẫn đang mở.",
                        "Thử lại với tên nút khác, hoặc dùng wf_get_ui_tree để xem các nút hiện có trên dialog.",
                        details: new { DialogTitle = title, AvailableButtons = buttons });
                }

                return new InteractionResult
                {
                    Success = true,
                    Message = $"Đã bấm nút '{btn.SafeLabel()}' để đóng modal dialog '{title}'."
                };
            }
        }

        var availableButtons = buttons != null ? string.Join(", ", buttons) : "không rõ";
        throw new ToolException(
            ErrorCode.ElementNotFound,
            $"Không tìm thấy nút '{buttonName}' trên modal dialog '{title}'.",
            $"Các nút có sẵn trên dialog: [{availableButtons}].");
    }

    /// <summary>Chờ tới khi handle dialog không còn là cửa sổ hiển thị.</summary>
    private static bool WaitUntilDialogClosed(IntPtr dialogHwnd, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            if (!IsWindow(dialogHwnd) || !IsWindowVisible(dialogHwnd))
            {
                return true;
            }
            Thread.Sleep(75);
        }
        while (DateTime.UtcNow < deadline);

        return !IsWindow(dialogHwnd) || !IsWindowVisible(dialogHwnd);
    }

    private bool WaitUntilNoModal(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        do
        {
            if (!_session.DetectBlockingModal().HasModal)
            {
                return true;
            }
            Thread.Sleep(75);
        }
        while (DateTime.UtcNow < deadline);

        return !_session.DetectBlockingModal().HasModal;
    }

    private void CheckModalBlock()
    {
        var (hasModal, _, _, title, text, buttons) = _session.DetectBlockingModal();
        if (hasModal)
        {
            var btnList = buttons != null && buttons.Count > 0 ? string.Join(", ", buttons) : "OK";
            throw new ToolException(
                ErrorCode.BlockedByModal,
                $"Thao tác bị chặn bởi modal dialog đang mở: \"{title}\" ({text}).",
                $"Hãy dùng 'wf_dialog_respond' với nút [{btnList}] để xử lý dialog trước.");
        }
    }

    private InteractionResult PostAction(string successMessage)
    {
        Thread.Sleep(100);

        var result = new InteractionResult
        {
            Success = true,
            Message = successMessage
        };

        // Check if action triggered a new modal dialog
        var (hasModal, _, _, title, text, buttons) = _session.DetectBlockingModal();
        if (hasModal)
        {
            var btnList = buttons != null && buttons.Count > 0 ? string.Join(", ", buttons) : "OK";
            result.Warnings.Add($"Thao tác vừa kích hoạt Modal Dialog mới: \"{title}\" (Nội dung: \"{text}\"). Cần gọi wf_dialog_respond [{btnList}] trước khi thao tác tiếp.");
        }

        return result;
    }

    private static string? GetElementValue(AutomationElement element)
    {
        try
        {
            if (element.Patterns.Value.IsSupported)
            {
                return element.Patterns.Value.Pattern.Value.Value;
            }
            if (element.Patterns.LegacyIAccessible.IsSupported)
            {
                return element.Patterns.LegacyIAccessible.Pattern.Value.Value;
            }
            if (element.Patterns.Text.IsSupported)
            {
                return element.Patterns.Text.Pattern.DocumentRange.GetText(-1);
            }
        }
        catch
        {
            // Ignore
        }

        return element.SafeName();
    }
}
