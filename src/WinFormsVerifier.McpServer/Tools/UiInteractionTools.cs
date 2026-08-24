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
        Nếu control CÓ trạng thái đọc được (Toggle/SelectionItem), trạng thái sẽ được đọc lại sau khi click và
        đưa vào 'warnings' nếu không đổi — đừng coi 'ok' là bằng chứng thao tác đã có tác dụng khi có warning.
        Ở chế độ chỉ-đọc (WFVERIFY_READONLY=1), control có nhãn khớp danh sách ghi dữ liệu (Ghi, Lưu, Xóa, Cập nhật, Duyệt...)
        sẽ bị chặn với lỗi READONLY_MODE.
        """)]
    public static async Task<CallToolResult> Invoke(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của control cần click (vd 'id:btnLogin' hoặc 'name:Đăng nhập').")]
        string selector,
        [Description("Selector của cửa sổ mục tiêu (tùy chọn). Chỉ khớp cửa sổ cấp cao nhất và form MDI child, KHÔNG khớp tab/panel — để giới hạn theo tab hãy dùng selector phân cấp.")]
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
        Bị chặn hoàn toàn (lỗi READONLY_MODE) khi server chạy với WFVERIFY_READONLY=1.
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
        Sau khi gọi TogglePattern, đọc lại ToggleState để xác minh; nếu chưa đổi thì click vật lý,
        vẫn chưa đổi thì trả về 'warnings' kèm trạng thái thực tế.
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
        Sau khi chọn, LUÔN đọc lại selection của container để xác minh; nếu không đổi thì thử click vật lý,
        vẫn không đổi thì trả về 'warnings' thay vì báo thành công trơn.
        Với combo của DevExpress/DotNetBar (lộ ra là Pane, không có ListItem con), tool tự click bung dropdown
        rồi tìm item trong cửa sổ popup riêng của ứng dụng.
        """)]
    public static async Task<CallToolResult> Select(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của ComboBox/ListBox/TabControl.")]
        string selector,
        [Description("Tên hoặc nhãn của item cần chọn (vd 'Hà Nội'). Khớp CHÍNH XÁC được ưu tiên; nếu chỉ khớp-chứa và trúng nhiều mục thì trả lỗi AMBIGUOUS kèm danh sách thay vì chọn nhầm.")]
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
        Với bảng nhiều dòng, dùng wf_grid_find để lọc theo cột thay vì kéo cả bảng về.
        Số cột được suy ra từ GridPattern, header, rồi tới số ô của dòng đầu tiên (grid bên thứ ba như C1FlexGrid
        không hỗ trợ GridPattern nên luôn trả ColumnCount = 0).
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

    [McpServerTool(Name = "wf_grid_find")]
    [Description("""
        Tìm các dòng trong DataGridView/Table theo điều kiện trên MỘT cột, trả về chỉ số dòng + nội dung dòng đó.
        Dùng thay cho wf_grid_read khi bảng nhiều dòng: không phải kéo cả bảng về rồi tự lọc.
        Trả về meta.matches (mảng {rowIndex, cells}) để dùng tiếp với wf_grid_set_cell hoặc selector 'grid:row,col'.
        """)]
    public static async Task<CallToolResult> GridFind(
        UiSession session,
        ElementLocator locator,
        InteractionService interaction,
        [Description("Selector của DataGridView (vd 'id:dgOrders').")]
        string selector,
        [Description("Tên cột (theo header, khớp chính xác trước rồi mới khớp chứa) hoặc chỉ số cột 0-based.")]
        string column,
        [Description("Giá trị cần tìm trong cột đó.")]
        string value,
        [Description("Cách so khớp: 'contains' (mặc định), 'equals', 'startswith'. Không phân biệt hoa thường.")]
        string op = "contains",
        [Description("Số dòng khớp tối đa trả về. Mặc định 20.")]
        int maxMatches = 20,
        [Description("Chỉ số dòng bắt đầu quét (0-based). Mặc định 0.")]
        int startRow = 0,
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
                return interaction.GridFind(element, column, value, op, maxMatches, startRow);
            }, TimeSpan.FromSeconds(30), ct);

            return McpResults.Ok(new { resultText = res.Message, meta = res.Data }, res.Warnings);
        });
    }

    [McpServerTool(Name = "wf_grid_set_cell")]
    [Description("""
        Chỉnh sửa giá trị của một ô (Cell) trong DataGridView theo chỉ số dòng (row) và cột (col).
        Bị chặn hoàn toàn (lỗi READONLY_MODE) khi server chạy với WFVERIFY_READONLY=1.
        """)]
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
