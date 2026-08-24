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
        ReadOnlyGuard.EnsureInvokeAllowed(element.SafeLabel());

        // Trạng thái TRƯỚC khi click: nếu control có trạng thái đọc được (Toggle/SelectionItem)
        // thì sau khi click phải đọc lại và so sánh, không báo "đã click" một cách vô điều kiện.
        var preToggle = SafeToggleState(element);
        var preSelected = SafeIsSelected(element);

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

        var result = PostAction($"Đã click/kích hoạt '{element.SafeLabel()}'.");
        VerifyInvokeEffect(element, preToggle, preSelected, result);
        return result;
    }

    /// <summary>
    /// Xác minh hậu điều kiện cho Invoke. Chỉ áp dụng khi control CÓ trạng thái đọc được —
    /// nút bấm thường không có hậu điều kiện quan sát được ở phía UIA.
    /// </summary>
    private static void VerifyInvokeEffect(AutomationElement element, ToggleState? preToggle, bool? preSelected, InteractionResult result)
    {
        if (preToggle.HasValue)
        {
            var after = SafeToggleState(element);
            if (after.HasValue && after.Value == preToggle.Value)
            {
                result.Warnings.Add(
                    $"Đã gọi Invoke nhưng ToggleState của '{element.SafeLabel()}' vẫn là '{after.Value}' — thao tác có thể chưa có tác dụng.");
            }
            return;
        }

        if (preSelected == false && SafeIsSelected(element) == false)
        {
            result.Warnings.Add(
                $"Đã gọi Invoke nhưng '{element.SafeLabel()}' vẫn chưa được chọn (IsSelected=false) — thao tác có thể chưa có tác dụng.");
        }
    }

    private static ToggleState? SafeToggleState(AutomationElement element)
    {
        try
        {
            return element.Patterns.Toggle.IsSupported ? element.Patterns.Toggle.Pattern.ToggleState.Value : null;
        }
        catch { return null; }
    }

    private static bool? SafeIsSelected(AutomationElement element)
    {
        try
        {
            return element.Patterns.SelectionItem.IsSupported ? element.Patterns.SelectionItem.Pattern.IsSelected.Value : null;
        }
        catch { return null; }
    }

    private static bool TryPhysicalClick(AutomationElement element)
    {
        try
        {
            element.Click();
            Thread.Sleep(150);
            return true;
        }
        catch { return false; }
    }

    public InteractionResult SetValue(AutomationElement element, string value, string mode = "replace", bool verify = true)
    {
        CheckModalBlock();
        ReadOnlyGuard.EnsureWriteAllowed("wf_set_value", element.SafeLabel());

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

            var wanted = target switch
            {
                "on" => ToggleState.On,
                "off" => ToggleState.Off,
                _ => current == ToggleState.On ? ToggleState.Off : ToggleState.On
            };

            if (current != wanted)
            {
                try { element.Patterns.Toggle.Pattern.Toggle(); }
                catch { /* xác minh bên dưới sẽ quyết định có fallback không */ }
                Thread.Sleep(100);
            }

            // Xác minh hậu điều kiện: TogglePattern.Toggle() có thể trả về mà trạng thái không đổi
            // (control bị disable logic, handler chặn). Đọc lại rồi mới báo kết quả.
            var after = SafeToggleState(element);
            if (after != wanted)
            {
                TryPhysicalClick(element);
                after = SafeToggleState(element);
            }

            var result = PostAction($"Đã chuyển trạng thái toggle của '{element.SafeLabel()}' sang '{after}'.");
            if (after != wanted)
            {
                result.Warnings.Add(
                    $"Đã gọi Toggle nhưng trạng thái của '{element.SafeLabel()}' vẫn là '{after}' (mong đợi '{wanted}') — kể cả sau khi click vật lý.");
            }

            return result;
        }

        if (element.Patterns.SelectionItem.IsSupported)
        {
            try { element.Patterns.SelectionItem.Pattern.Select(); }
            catch { /* xác minh bên dưới */ }
            Thread.Sleep(100);

            if (SafeIsSelected(element) == false)
            {
                TryPhysicalClick(element);
            }

            var result = PostAction($"Đã chọn '{element.SafeLabel()}'.");
            if (SafeIsSelected(element) == false)
            {
                result.Warnings.Add($"Đã gọi Select nhưng '{element.SafeLabel()}' vẫn báo IsSelected=false.");
            }

            return result;
        }

        return Invoke(element);
    }

    public InteractionResult Select(AutomationElement element, string? item = null, int? index = null)
    {
        CheckModalBlock();

        var containerLabel = element.SafeLabel();

        // Bản thân element đã là một mục chọn được (ListItem / TabItem) và không có tiêu chí con.
        if (element.Patterns.SelectionItem.IsSupported && string.IsNullOrEmpty(item) && !index.HasValue)
        {
            try { element.Patterns.SelectionItem.Pattern.Select(); }
            catch { /* xác minh bên dưới quyết định fallback */ }
            Thread.Sleep(100);

            if (SafeIsSelected(element) == false)
            {
                TryPhysicalClick(element);
            }

            var selfResult = PostAction($"Đã chọn mục '{containerLabel}'.");
            if (SafeIsSelected(element) == false)
            {
                selfResult.Warnings.Add(
                    $"Đã gọi Select nhưng '{containerLabel}' vẫn báo IsSelected=false — selection có thể không thay đổi.");
            }

            return selfResult;
        }

        if (string.IsNullOrEmpty(item) && !index.HasValue)
        {
            throw new ToolException(
                ErrorCode.Internal,
                $"Cần cung cấp ít nhất tham số 'item' hoặc 'index' để chọn mục trong '{containerLabel}'.");
        }

        // Container (ComboBox / ListBox / TabControl)
        if (element.Patterns.ExpandCollapse.IsSupported && element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
        {
            try
            {
                element.Patterns.ExpandCollapse.Pattern.Expand();
                Thread.Sleep(100);
            }
            catch { /* continue */ }
        }

        var children = element
            .FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem).Or(cf.ByControlType(ControlType.TabItem)))
            .ToList();

        var viaPopup = false;
        if (children.Count == 0)
        {
            // Combo của DevExpress / DotNetBar lộ ra là Pane: không Selection, không ExpandCollapse,
            // và dropdown là POPUP WINDOW riêng của cùng process nên KHÔNG nằm trong cây con của form.
            // Bung dropdown bằng click vật lý rồi tìm item trong các cửa sổ mới xuất hiện.
            children = OpenDropdownAndCollectItems(element);
            viaPopup = children.Count > 0;
        }

        if (children.Count == 0)
        {
            var patterns = ElementDto.DetectSupportedPatterns(element);
            throw new ToolException(
                ErrorCode.ElementNotFound,
                $"Không tìm thấy mục nào trong '{containerLabel}' ({element.SafeControlTypeName()}): cây UIA không có ListItem/TabItem và click bung dropdown cũng không tạo popup nào.",
                "Nếu đây là combo của thư viện bên thứ ba, thử wf_focus rồi wf_send_keys ('{DOWN}', '{ENTER}') " +
                "hoặc wf_set_value nếu control cho nhập text trực tiếp.",
                details: new { Patterns = patterns });
        }

        var names = children.Select(c => c.SafeName()).ToList();

        try
        {
            AutomationElement target;
            string expectedName;

            if (index.HasValue)
            {
                if (index.Value < 0 || index.Value >= children.Count)
                {
                    throw new ToolException(
                        ErrorCode.ElementNotFound,
                        $"index {index.Value} nằm ngoài phạm vi: danh sách '{containerLabel}' có {children.Count} mục" +
                        (children.Count > 0 ? $" (index hợp lệ 0..{children.Count - 1})." : "."),
                        children.Count > 0
                            ? $"Các mục hiện có: [{string.Join(", ", names)}]."
                            : "Danh sách đang rỗng — có thể dữ liệu chưa được nạp, hãy chờ bằng wf_wait_idle rồi thử lại.");
                }

                target = children[index.Value];
                expectedName = names[index.Value];
            }
            else
            {
                var match = ItemMatcher.Match(names, item!);
                switch (match.Kind)
                {
                    case ItemMatchKind.Ambiguous:
                        var ambiguous = match.AmbiguousIndexes.Select(i => $"[{i}] {names[i]}").ToList();
                        throw new ToolException(
                            ErrorCode.Ambiguous,
                            $"Từ khoá '{item}' khớp {ambiguous.Count} mục trong '{containerLabel}': [{string.Join(", ", ambiguous)}].",
                            "Truyền đúng tên đầy đủ của mục (khớp chính xác được ưu tiên) hoặc dùng tham số 'index'.",
                            details: new { Matches = ambiguous });

                    case ItemMatchKind.NotFound:
                        throw new ToolException(
                            ErrorCode.ElementNotFound,
                            $"Không tìm thấy mục '{item}' trong danh sách '{containerLabel}'.",
                            $"Danh sách các mục có sẵn: [{string.Join(", ", names)}]");
                }

                target = children[match.Index];
                expectedName = names[match.Index];
            }

            return SelectTargetWithVerify(element, target, expectedName, containerLabel, viaPopup);
        }
        catch
        {
            // Không để dropdown vừa bung nằm lại chắn màn hình khi lời gọi thất bại.
            if (viaPopup)
            {
                try { Keyboard.Type(VirtualKeyShort.ESCAPE); } catch { /* ignore */ }
            }
            throw;
        }
    }

    /// <summary>
    /// Bung dropdown bằng click vật lý rồi thu thập item trong các cửa sổ MỚI của chính process đích.
    /// Chỉ duyệt cửa sổ theo PID (EnumWindows + lọc), không đi qua desktop của UIA.
    /// </summary>
    private List<AutomationElement> OpenDropdownAndCollectItems(AutomationElement element)
    {
        var pid = _session.ProcessId;
        if (pid == null) return new List<AutomationElement>();

        var before = ProcessWindowHandles(pid.Value);
        if (!TryPhysicalClick(element)) return new List<AutomationElement>();

        var deadline = DateTime.UtcNow.AddMilliseconds(1500);
        do
        {
            foreach (var hwnd in ProcessWindowHandles(pid.Value).Except(before))
            {
                try
                {
                    var popup = _session.Automation.FromHandle(hwnd);
                    if (popup == null) continue;

                    var items = popup.FindAllDescendants(cf =>
                        cf.ByControlType(ControlType.ListItem)
                          .Or(cf.ByControlType(ControlType.DataItem))
                          .Or(cf.ByControlType(ControlType.TreeItem)));

                    if (items.Length > 0) return items.ToList();
                }
                catch { /* cửa sổ vừa đóng hoặc không truy cập được */ }
            }

            Thread.Sleep(100);
        }
        while (DateTime.UtcNow < deadline);

        return new List<AutomationElement>();
    }

    private static HashSet<IntPtr> ProcessWindowHandles(int processId)
    {
        return NativeWindows.GetProcessWindows(processId).Select(w => w.Hwnd).ToHashSet();
    }

    private InteractionResult SelectTargetWithVerify(
        AutomationElement container,
        AutomationElement target,
        string expectedName,
        string containerLabel,
        bool viaPopup)
    {
        var selected = false;
        if (target.Patterns.SelectionItem.IsSupported)
        {
            try
            {
                target.Patterns.SelectionItem.Pattern.Select();
                selected = true;
            }
            catch { /* rơi xuống click vật lý */ }
        }

        if (!selected)
        {
            TryPhysicalClick(target);
        }

        Thread.Sleep(120);

        // Hậu điều kiện BẮT BUỘC: đọc lại selection của container. Trước đây tool báo
        // "Đã chọn mục X" ngay sau khi gọi Select() mà không đọc lại -> test pass giả.
        var (ok, actual) = VerifySelection(container, expectedName);
        if (!ok)
        {
            TryPhysicalClick(target);
            (ok, actual) = VerifySelection(container, expectedName);
        }

        var result = PostAction($"Đã chọn mục '{expectedName}' trong '{containerLabel}'{(viaPopup ? " (qua dropdown popup)" : "")}.");
        if (!ok)
        {
            result.Warnings.Add(
                $"Đã gọi Select nhưng selection của '{containerLabel}' không đổi thành '{expectedName}' " +
                $"(đọc lại được: '{actual}') — kể cả sau khi click vật lý.");
        }

        return result;
    }

    private static (bool Ok, string? Actual) VerifySelection(AutomationElement container, string expectedName)
    {
        try
        {
            if (container.Patterns.Selection.IsSupported)
            {
                var selection = container.Patterns.Selection.Pattern.Selection.ValueOrDefault;
                if (selection is { Length: > 0 })
                {
                    var selectedNames = selection.Select(s => s.SafeName()).ToList();
                    var matched = selectedNames.Any(n => string.Equals(n?.Trim(), expectedName?.Trim(), StringComparison.OrdinalIgnoreCase));
                    return (matched, string.Join(", ", selectedNames));
                }
            }
        }
        catch { /* rơi xuống đọc giá trị hiển thị */ }

        var value = GetElementValue(container) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expectedName)) return (true, value);

        var ok = string.Equals(value.Trim(), expectedName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 value.Contains(expectedName.Trim(), StringComparison.OrdinalIgnoreCase);
        return (ok, value);
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
        var grid = new GridAccessor(element);
        var sb = new StringBuilder();

        int endRow = Math.Min(grid.RowCount, Math.Max(0, startRow) + maxRows);
        int colsToRead = Math.Min(grid.ColumnCount > 0 ? grid.ColumnCount : 20, maxCols);

        sb.AppendLine($"Bảng: {grid.RowCount} dòng × {grid.ColumnCount} cột (Hiển thị từ dòng {startRow} đến {Math.Max(0, endRow - 1)}):");
        if (grid.Headers.Count > 0)
        {
            sb.AppendLine("Cột   | " + string.Join(" | ", grid.Headers.Take(colsToRead).Select(h => h.PadRight(15))));
        }
        sb.AppendLine(new string('-', 60));

        for (int r = startRow; r < endRow; r++)
        {
            var cellValues = new List<string>();
            for (int c = 0; c < colsToRead; c++)
            {
                cellValues.Add(grid.GetCellText(r, c).PadRight(15));
            }
            sb.AppendLine($"Row {r:D2} | " + string.Join(" | ", cellValues));
        }

        return new InteractionResult
        {
            Success = true,
            Message = sb.ToString(),
            Data = new
            {
                TotalRows = grid.RowCount,
                TotalCols = grid.ColumnCount,
                DisplayedRows = Math.Max(0, endRow - startRow),
                Headers = grid.Headers
            }
        };
    }

    /// <summary>
    /// Tìm dòng theo điều kiện trên một cột thay vì kéo cả bảng về rồi lọc phía agent.
    /// </summary>
    public InteractionResult GridFind(
        AutomationElement element,
        string column,
        string value,
        string op = "contains",
        int maxMatches = 20,
        int startRow = 0)
    {
        var grid = new GridAccessor(element);

        var colIndex = grid.ResolveColumnIndex(column);
        if (colIndex < 0)
        {
            throw new ToolException(
                ErrorCode.ElementNotFound,
                $"Không xác định được cột '{column}' trong bảng '{element.SafeLabel()}'.",
                grid.Headers.Count > 0
                    ? $"Các cột hiện có: [{string.Join(", ", grid.Headers)}]. Có thể truyền tên cột hoặc chỉ số 0-based."
                    : "Bảng không lộ tên cột qua UIA — hãy truyền chỉ số cột 0-based.");
        }

        var comparison = op.Trim().ToLowerInvariant();
        if (comparison is not ("contains" or "equals" or "startswith"))
        {
            throw new ToolException(
                ErrorCode.Internal,
                $"Toán tử so khớp '{op}' không hợp lệ.",
                "Chỉ hỗ trợ 'contains', 'equals', 'startswith'.");
        }

        var matches = new List<object>();
        var sb = new StringBuilder();
        int scanned = 0;

        for (int r = Math.Max(0, startRow); r < grid.RowCount && matches.Count < maxMatches; r++)
        {
            scanned++;
            var cellText = grid.GetCellText(r, colIndex);

            var hit = comparison switch
            {
                "equals" => string.Equals(cellText.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase),
                "startswith" => cellText.TrimStart().StartsWith(value.Trim(), StringComparison.OrdinalIgnoreCase),
                _ => cellText.Contains(value, StringComparison.OrdinalIgnoreCase)
            };

            if (!hit) continue;

            var rowCells = new List<string>();
            for (int c = 0; c < grid.ColumnCount; c++)
            {
                rowCells.Add(grid.GetCellText(r, c));
            }

            matches.Add(new { RowIndex = r, Cells = rowCells });
            sb.AppendLine($"Row {r:D2} | " + string.Join(" | ", rowCells.Select(x => x.PadRight(15))));
        }

        var header = $"Tìm '{value}' ({comparison}) trên cột '{(grid.Headers.Count > colIndex ? grid.Headers[colIndex] : colIndex.ToString())}': " +
                     $"{matches.Count} dòng khớp / {scanned} dòng đã quét (tổng {grid.RowCount} dòng).";

        return new InteractionResult
        {
            Success = true,
            Message = matches.Count > 0 ? header + Environment.NewLine + sb : header,
            Data = new
            {
                ColumnIndex = colIndex,
                TotalRows = grid.RowCount,
                ScannedRows = scanned,
                MatchCount = matches.Count,
                Headers = grid.Headers,
                Matches = matches
            }
        };
    }

    public InteractionResult GridSetCell(AutomationElement element, int row, int col, string value)
    {
        CheckModalBlock();
        ReadOnlyGuard.EnsureWriteAllowed("wf_grid_set_cell", $"{element.SafeLabel()}[{row},{col}]");

        var grid = new GridAccessor(element);
        var cell = grid.GetCell(row, col);
        if (cell == null)
        {
            throw new ToolException(
                ErrorCode.ElementNotFound,
                $"Không tìm thấy ô tại dòng {row}, cột {col}.",
                $"Bảng đang có {grid.RowCount} dòng × {grid.ColumnCount} cột.");
        }

        return SetValue(cell, value);
    }

    /// <summary>
    /// Bọc một bảng và CACHE row/cell. Trước đây mỗi ô gọi lại AsDataGridView() + dựng lại
    /// mảng Rows: đọc 50×20 tốn 1000 vòng UIA thừa. Cũng suy ra số cột từ dòng đầu tiên khi
    /// control không hỗ trợ GridPattern (C1FlexGrid luôn trả ColumnCount = 0).
    /// </summary>
    private sealed class GridAccessor
    {
        private readonly AutomationElement _grid;
        private readonly DataGridViewRow[] _rows;
        private readonly Dictionary<int, AutomationElement[]> _cellCache = new();
        private readonly AutomationElement[]? _fallbackRows;

        public int RowCount { get; }
        public int ColumnCount { get; }
        public IReadOnlyList<string> Headers { get; }

        public GridAccessor(AutomationElement grid)
        {
            _grid = grid;

            int patternRows = 0, patternCols = 0;
            try
            {
                if (grid.Patterns.Grid.IsSupported)
                {
                    patternRows = grid.Patterns.Grid.Pattern.RowCount.ValueOrDefault;
                    patternCols = grid.Patterns.Grid.Pattern.ColumnCount.ValueOrDefault;
                }
            }
            catch { /* ignore */ }

            _rows = SafeRows(grid);
            _fallbackRows = _rows.Length == 0
                ? grid.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem).Or(cf.ByControlType(ControlType.Custom)))
                : null;

            Headers = SafeHeaders(grid);

            RowCount = patternRows > 0
                ? patternRows
                : (_rows.Length > 0 ? _rows.Length : _fallbackRows?.Length ?? 0);

            // Thứ tự suy luận số cột: GridPattern -> header -> số ô của dòng đầu tiên.
            ColumnCount = patternCols > 0
                ? patternCols
                : (Headers.Count > 0 ? Headers.Count : FirstRowCellCount());
        }

        private int FirstRowCellCount()
        {
            var cells = CellsOf(0);
            return cells?.Length ?? 0;
        }

        private static DataGridViewRow[] SafeRows(AutomationElement grid)
        {
            try { return grid.AsDataGridView()?.Rows ?? Array.Empty<DataGridViewRow>(); }
            catch { return Array.Empty<DataGridViewRow>(); }
        }

        private static IReadOnlyList<string> SafeHeaders(AutomationElement grid)
        {
            try
            {
                var header = grid.AsDataGridView()?.Header;
                if (header?.Columns is { Length: > 0 } columns)
                {
                    return columns.Select(c =>
                    {
                        try { return c.Text ?? string.Empty; }
                        catch { return string.Empty; }
                    }).ToList();
                }
            }
            catch { /* ignore */ }

            try
            {
                var headerItems = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.HeaderItem));
                if (headerItems.Length > 0)
                {
                    return headerItems.Select(h => h.SafeName()).ToList();
                }
            }
            catch { /* ignore */ }

            return Array.Empty<string>();
        }

        private AutomationElement[]? CellsOf(int row)
        {
            if (row < 0) return null;
            if (_cellCache.TryGetValue(row, out var cached)) return cached;

            AutomationElement[]? cells = null;
            try
            {
                if (row < _rows.Length)
                {
                    cells = _rows[row].Cells.Cast<AutomationElement>().ToArray();
                }
                else if (_fallbackRows != null && row < _fallbackRows.Length)
                {
                    cells = _fallbackRows[row].FindAllChildren();
                }
            }
            catch { cells = null; }

            if (cells != null)
            {
                _cellCache[row] = cells;
            }

            return cells;
        }

        public AutomationElement? GetCell(int row, int col)
        {
            if (row < 0 || col < 0) return null;

            var cells = CellsOf(row);
            if (cells != null && col < cells.Length) return cells[col];

            try
            {
                if (_grid.Patterns.Grid.IsSupported)
                {
                    return _grid.Patterns.Grid.Pattern.GetItem(row, col);
                }
            }
            catch { /* WinForms DataGridView ném NotImplementedException */ }

            return null;
        }

        public string GetCellText(int row, int col)
        {
            var cell = GetCell(row, col);
            if (cell == null) return string.Empty;
            return GetElementValue(cell) ?? cell.SafeName();
        }

        /// <summary>Cột nhận theo tên (khớp chính xác rồi mới khớp chứa) hoặc theo chỉ số 0-based.</summary>
        public int ResolveColumnIndex(string column)
        {
            if (int.TryParse(column.Trim(), out var byIndex))
            {
                return byIndex >= 0 && (ColumnCount == 0 || byIndex < ColumnCount) ? byIndex : -1;
            }

            var match = ItemMatcher.Match(Headers, column);
            return match.Kind is ItemMatchKind.Exact or ItemMatchKind.Contains ? match.Index : -1;
        }
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
