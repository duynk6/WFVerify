using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services;

namespace WinFormsVerifier.Tools;

[McpServerToolType]
public static class UiInteractionTools
{
    [McpServerTool(Name = "wf_invoke")]
    [Description("""
        Click hoặc kích hoạt một control (Button, Link, ToolStripButton, CheckBox, ...) thông qua chuỗi fallback thông minh:
        InvokePattern -> SelectionItem.Select -> LegacyIAccessible.DoDefaultAction -> Click vật lý.
        Sau khi click, tự động phát hiện nếu có Modal Dialog mới xuất hiện để cảnh báo.
        """)]
    public static async Task<CallToolResult> Invoke(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của control cần click (vd 'id:btnLogin' hoặc 'name:Đăng nhập').")]
        string selector,
        [Description("Selector của cửa sổ mục tiêu (tùy chọn).")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.Invoke(element);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message, data = res.Data }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_set_value")]
    [Description("""
        Nhập dữ liệu text vào TextBox, Edit control, MaskedTextBox.
        Tự động fallback sang Focus + SendKeys nếu control không hỗ trợ ValuePattern trực tiếp.
        Nếu 'verify=true', sẽ đọc lại giá trị sau khi gán và cảnh báo nếu không khớp.
        """)]
    public static async Task<CallToolResult> SetValue(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của TextBox cần nhập (vd 'id:txtUsername').")]
        string selector,
        [Description("Chuỗi giá trị cần nhập.")]
        string value,
        [Description("Chế độ nhập: 'replace' (xóa hết rồi nhập mới) hoặc 'append' (nối tiếp vào cuối). Mặc định 'replace'.")]
        string mode = "replace",
        [Description("Đọc lại giá trị sau khi nhập để xác thực. Mặc định true.")]
        bool verify = true,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.SetValue(element, value, mode, verify);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message, data = res.Data }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_toggle")]
    [Description("""
        Bật/tắt trạng thái của CheckBox hoặc RadioButton.
        Hỗ trợ các trạng thái 'on', 'off', hoặc 'toggle'.
        """)]
    public static async Task<CallToolResult> Toggle(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của CheckBox/RadioButton.")]
        string selector,
        [Description("Trạng thái mong muốn: 'on', 'off', hoặc 'toggle'. Mặc định 'toggle'.")]
        string state = "toggle",
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.Toggle(element, state);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_select")]
    [Description("""
        Chọn một mục trong ComboBox, ListBox, ListView, TabControl hoặc TreeView theo tên item hoặc chỉ số index.
        """)]
    public static async Task<CallToolResult> Select(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của ComboBox/ListBox/TabControl.")]
        string selector,
        [Description("Tên hoặc nhãn của item cần chọn (vd 'Hà Nội').")]
        string? item = null,
        [Description("Chỉ số index (0-based) của item cần chọn.")]
        int? index = null,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.Select(element, item, index);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_expand")]
    [Description("""
        Mở rộng (Expand) hoặc thu gọn (Collapse) một TreeView node, ComboBox dropdown hoặc GroupBox có hỗ trợ ExpandCollapse.
        """)]
    public static async Task<CallToolResult> Expand(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của control cần mở/thu gọn.")]
        string selector,
        [Description("True để mở rộng, False để thu gọn. Mặc định true.")]
        bool expand = true,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.Expand(element, expand);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_send_keys")]
    [Description("""
        Gửi chuỗi phím bấm bàn phím thô tới ứng dụng hoặc control cụ thể (sử dụng FlaUI Keyboard / SendKeys).
        Hỗ trợ các phím điều hướng và tổ hợp phím tắt: {ENTER}, {TAB}, {ESC}, ^s (Ctrl+S), %{F4} (Alt+F4).
        """)]
    public static async Task<CallToolResult> SendKeys(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Chuỗi phím cần gửi (vd 'Hello World{ENTER}' hoặc '{TAB}').")]
        string keys,
        [Description("Selector của control cần nhận focus trước khi gõ phím (tùy chọn).")]
        string? selector = null,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = !string.IsNullOrWhiteSpace(selector)
                    ? locator.Resolve(window, selector, TimeSpan.FromSeconds(5))
                    : null;
                return interaction.SendKeys(keys, element);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_focus")]
    [Description("Đặt tiêu điểm (Focus) vào một control cụ thể.")]
    public static async Task<CallToolResult> Focus(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của control cần focus.")]
        string selector,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.Focus(element);
            }, TimeSpan.FromSeconds(10), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_scroll_into_view")]
    [Description("Cuộn danh sách/container tới khi control mục tiêu xuất hiện trên màn hình (ScrollItemPattern).")]
    public static async Task<CallToolResult> ScrollIntoView(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của control cần cuộn tới.")]
        string selector,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.ScrollIntoView(element);
            }, TimeSpan.FromSeconds(10), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_grid_read")]
    [Description("""
        Đọc dữ liệu từ DataGridView / Table thành bảng text có cấu trúc dòng/cột.
        Dùng tool này THAY VÌ bắt AI Vision đọc từ ảnh screenshot để đảm bảo độ chính xác 100% và tiết kiệm token.
        """)]
    public static async Task<CallToolResult> GridRead(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của DataGridView (vd 'id:dgOrders').")]
        string selector,
        [Description("Chỉ số dòng bắt đầu đọc (0-based). Mặc định 0.")]
        int startRow = 0,
        [Description("Số lượng dòng tối đa đọc trong một lần gọi. Mặc định 50.")]
        int maxRows = 50,
        [Description("Số lượng cột tối đa đọc. Mặc định 20.")]
        int maxCols = 20,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.GridRead(element, startRow, maxRows, maxCols);
            }, TimeSpan.FromSeconds(20), ct);

            return McpResults.Ok(new { tableText = res.Message, meta = res.Data }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_grid_set_cell")]
    [Description("Chỉnh sửa giá trị của một ô (Cell) trong DataGridView theo chỉ số dòng (row) và cột (col).")]
    public static async Task<CallToolResult> GridSetCell(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của DataGridView.")]
        string selector,
        [Description("Chỉ số dòng (0-based).")]
        int row,
        [Description("Chỉ số cột (0-based).")]
        int col,
        [Description("Giá trị mới cần nhập vào ô.")]
        string value,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return interaction.GridSetCell(element, row, col, value);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_menu_click")]
    [Description("""
        Nhấp chọn một mục trong MenuStrip hoặc ContextMenuStrip theo đường dẫn phân cấp (vd: 'File > Mở > Gần đây' hoặc 'Edit > Copy').
        Tự động mở rộng từng cấp menu con cho đến khi click vào mục lá.
        """)]
    public static async Task<CallToolResult> MenuClick(
        UiSession session,
        InteractionService interaction,
        [Description("Đường dẫn menu phân tách bởi dấu '>' hoặc '/', ví dụ 'File > Save' hoặc 'Báo cáo > Doanh thu > Tháng'.")]
        string menuPath,
        [Description("Selector của cửa sổ mục tiêu.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                return interaction.MenuClick(window, menuPath);
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_dialog_respond")]
    [Description("""
        Phản hồi và đóng một Modal Dialog (MessageBox, Form con dạng ShowDialog) đang chặn ứng dụng.
        Tự động tìm nút bấm trên dialog có tên khớp với 'buttonName' (vd 'OK', 'Cancel', 'Yes', 'No', 'Đồng ý', 'Hủy') và click.
        """)]
    public static async Task<CallToolResult> DialogRespond(
        UiSession session,
        InteractionService interaction,
        [Description("Tên hoặc nhãn của nút bấm cần click trên dialog (vd 'OK', 'Cancel', 'Yes', 'No').")]
        string buttonName = "OK",
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var res = await session.RunAsync(() =>
            {
                return interaction.DialogRespond(buttonName);
            }, TimeSpan.FromSeconds(10), ct);

            return McpResults.Ok(new { message = res.Message }, res.Warnings);
        });
    }
}
