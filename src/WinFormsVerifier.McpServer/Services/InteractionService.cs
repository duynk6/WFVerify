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

        try
        {
            var hwnd = element.Properties.NativeWindowHandle.ValueOrDefault;
            if (hwnd != IntPtr.Zero)
            {
                PostMessage(hwnd, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                success = true;
            }
            else
            {
                var rect = element.BoundingRectangle;
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
                $"Control '{element.Name}' ({element.ControlType}) không hỗ trợ thao tác Invoke.",
                $"Các pattern khả dụng: [{string.Join(", ", patterns)}].",
                details: new { Patterns = patterns });
        }

        return PostAction($"Đã click/kích hoạt '{element.Name ?? element.AutomationId ?? element.ControlType.ToString()}'.");
    }

    public InteractionResult SetValue(AutomationElement element, string value, string mode = "replace", bool verify = true)
    {
        CheckModalBlock();

        var targetValue = value;
        if (mode.Equals("append", StringComparison.OrdinalIgnoreCase))
        {
            var current = GetElementValue(element);
            targetValue = current + value;
        }

        var setSuccess = false;
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
                element.Focus();
                Thread.Sleep(50);
                if (mode.Equals("replace", StringComparison.OrdinalIgnoreCase))
                {
                    Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                    Keyboard.Type(VirtualKeyShort.BACK);
                }
                Keyboard.Type(targetValue);
                setSuccess = true;
            }
            catch (Exception ex)
            {
                throw new ToolException(
                    ErrorCode.PatternUnsupported,
                    $"Không thể nhập giá trị vào control '{element.Name}': {ex.Message}",
                    "Hãy kiểm tra xem control có bị Read-Only hoặc disabled không.");
            }
        }

        var res = PostAction($"Đã đặt giá trị cho '{element.Name ?? element.AutomationId}' thành '{targetValue}'.");

        if (verify)
        {
            var readBack = GetElementValue(element);
            if (!string.Equals(readBack, targetValue, StringComparison.Ordinal))
            {
                res.Warnings.Add($"Giá trị đọc lại sau khi set ('{readBack}') không khớp hoàn toàn với giá trị đã đặt ('{targetValue}').");
            }
        }

        return res;
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

            return PostAction($"Đã chuyển trạng thái toggle của '{element.Name}' sang '{element.Patterns.Toggle.Pattern.ToggleState.Value}'.");
        }

        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return PostAction($"Đã chọn '{element.Name}'.");
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
            return PostAction($"Đã chọn mục '{element.Name}'.");
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
            return PostAction($"Đã chọn index {index.Value} ('{target.Name}') trong '{element.Name}'.");
        }

        if (!string.IsNullOrEmpty(item))
        {
            var matched = children.FirstOrDefault(c => c.Name?.Contains(item, StringComparison.OrdinalIgnoreCase) == true);
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
                return PostAction($"Đã chọn mục '{matched.Name}' trong '{element.Name}'.");
            }

            throw new ToolException(
                ErrorCode.ElementNotFound,
                $"Không tìm thấy mục '{item}' trong danh sách '{element.Name}'.",
                $"Danh sách các mục có sẵn: [{string.Join(", ", children.Select(c => c.Name))}]");
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
            return PostAction($"Đã {(expand ? "mở rộng" : "thu gọn")} '{element.Name}'.");
        }

        throw new ToolException(ErrorCode.PatternUnsupported, $"Control '{element.Name}' không hỗ trợ ExpandCollapse.");
    }

    public InteractionResult Focus(AutomationElement element)
    {
        element.Focus();
        return PostAction($"Đã focus vào '{element.Name ?? element.AutomationId}'.");
    }

    public InteractionResult ScrollIntoView(AutomationElement element)
    {
        if (element.Patterns.ScrollItem.IsSupported)
        {
            element.Patterns.ScrollItem.Pattern.ScrollIntoView();
            return PostAction($"Đã cuộn tới '{element.Name}'.");
        }

        throw new ToolException(ErrorCode.PatternUnsupported, $"Control '{element.Name}' không hỗ trợ ScrollItem.");
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
                    cellText = GetElementValue(cell) ?? cell.Name ?? "";
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
                    .FirstOrDefault(m => m.Name?.Contains(seg, StringComparison.OrdinalIgnoreCase) == true);
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

    public InteractionResult DialogRespond(string buttonName)
    {
        var (hasModal, dialogHwnd, modalWindow, title, text, buttons) = _session.DetectBlockingModal();
        if (!hasModal)
        {
            throw new ToolException(
                ErrorCode.WindowNotFound,
                "Không có modal dialog nào đang mở để phản hồi.",
                "Sử dụng wf_list_windows để kiểm tra các cửa sổ đang hoạt động.");
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
                Thread.Sleep(300);
                return new InteractionResult
                {
                    Success = true,
                    Message = $"Đã bấm nút '{foundName}' để đóng modal dialog '{title}'."
                };
            }
        }

        if (modalWindow != null)
        {
            var btn = modalWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(buttonName)));
            if (btn == null)
            {
                btn = modalWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
                    .FirstOrDefault(b => b.Name?.Contains(buttonName, StringComparison.OrdinalIgnoreCase) == true);
            }

            if (btn != null)
            {
                btn.Click();
                Thread.Sleep(300);
                return new InteractionResult
                {
                    Success = true,
                    Message = $"Đã bấm nút '{btn.Name}' để đóng modal dialog '{title}'."
                };
            }
        }

        var availableButtons = buttons != null ? string.Join(", ", buttons) : "không rõ";
        throw new ToolException(
            ErrorCode.ElementNotFound,
            $"Không tìm thấy nút '{buttonName}' trên modal dialog '{title}'.",
            $"Các nút có sẵn trên dialog: [{availableButtons}].");
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

        return element.Name;
    }
}
