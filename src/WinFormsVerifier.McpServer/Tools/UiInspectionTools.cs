using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services;

namespace WinFormsVerifier.Tools;

[McpServerToolType]
public static class UiInspectionTools
{
    [McpServerTool(Name = "wf_get_ui_tree")]
    [Description("""
        Lấy cây phân cấp UI của cửa sổ WinForms đang chạy dưới định dạng text thụt lề siêu gọn gàng (tiết kiệm token context).
        Luôn gọi tool này ĐẦU TIÊN sau khi mở app để quan sát layout và xác định selector chính xác cho các control.
        """)]
    public static async Task<CallToolResult> GetUiTree(
        UiSession session,
        TreeSerializer serializer,
        [Description("Selector chỉ định cửa sổ cần lấy cây (vd 'name~:Đơn hàng'). Bỏ trống = Modal dialog đang active, nếu không có thì Main Window.")]
        string? windowSelector = null,
        [Description("Độ sâu tối đa của cây phân cấp. Mặc định 5.")]
        int maxDepth = 5,
        [Description("Lọc theo danh sách ControlType phân tách bởi dấu phẩy (vd 'Button,Edit,ComboBox,DataGrid').")]
        string? filterTypes = null,
        [Description("Số lượng node tối đa được trả về để tránh nổ token context. Mặc định 300.")]
        int maxNodes = 300,
        [Description("Bao gồm cả các control đang ẩn hoặc off-screen.")]
        bool includeInvisible = false,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var result = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                return serializer.Serialize(window, maxDepth, filterTypes, maxNodes, includeInvisible);
            }, TimeSpan.FromSeconds(20), ct);

            return McpResults.Ok(result.Text, result.Warnings);
        });
    }

    [McpServerTool(Name = "wf_find_elements")]
    [Description("""
        Tìm kiếm danh sách các element thỏa mãn selector trong cửa sổ mục tiêu.
        Hỗ trợ các selector như 'type:Button', 'name~:Lưu', 'class:WindowsForms10.EDIT.app.0.xxx'.
        Trả về danh sách ElementDto gọn nhẹ gồm ID, Name, Type, Bounds.
        """)]
    public static async Task<CallToolResult> FindElements(
        UiSession session,
        ElementLocator locator,
        [Description("Selector tìm kiếm (vd 'type:Button' hoặc 'id:txtUsername').")]
        string selector,
        [Description("Selector của cửa sổ mục tiêu. Bỏ trống = cửa sổ hiện tại / active modal.")]
        string? windowSelector = null,
        [Description("Số lượng element tối đa trả về. Mặc định 20.")]
        int limit = 20,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var elements = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var matches = locator.ResolveAll(window, selector, limit);
                return matches.Select(ElementDto.FromAutomationElement).ToList();
            }, TimeSpan.FromSeconds(15), ct);

            return McpResults.Ok(new
            {
                count = elements.Count,
                elements = elements
            });
        });
    }

    [McpServerTool(Name = "wf_get_element")]
    [Description("""
        Lấy thông tin chi tiết của MỘT element cụ thể theo selector, bao gồm:
        ID, Name, ControlType, Value, Enabled, Bounds, và DANH SÁCH CÁC PATTERN KHẢ DỤNG (Invoke, Value, Toggle, Grid, ...).
        Dùng tool này để kiểm tra xem một control có hỗ trợ thao tác bạn dự định thực hiện hay không.
        """)]
    public static async Task<CallToolResult> GetElement(
        UiSession session,
        ElementLocator locator,
        [Description("Selector của control cần kiểm tra (vd 'id:btnSave' hoặc 'name:Đăng nhập').")]
        string selector,
        [Description("Selector của cửa sổ mục tiêu. Bỏ trống = cửa sổ hiện tại.")]
        string? windowSelector = null,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var elementDto = await session.RunAsync(() =>
            {
                var window = session.ResolveWindow(windowSelector);
                var element = locator.Resolve(window, selector, TimeSpan.FromSeconds(5));
                return ElementDto.FromAutomationElement(element);
            }, TimeSpan.FromSeconds(10), ct);

            return McpResults.Ok(elementDto);
        });
    }
}
