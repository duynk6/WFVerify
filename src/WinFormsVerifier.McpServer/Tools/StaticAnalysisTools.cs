using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services.Roslyn;

namespace WinFormsVerifier.Tools;

[McpServerToolType]
public static class StaticAnalysisTools
{
    [McpServerTool(Name = "wf_analyze_form")]
    [Description("""
        Phân tích tĩnh (Static Analysis) một Form WinForms dựa trên cụm partial class (Form.cs + Form.Designer.cs) bằng Roslyn.
        Phát hiện các lỗi layout tiềm ẩn, trùng TabIndex, event handler mồ côi (WF001/WF002), xung đột Dock/Anchor (WF030), và thiếu AccessibleName (WF040).
        """)]
    public static async Task<CallToolResult> AnalyzeForm(
        FormAnalyzer analyzer,
        [Description("Đường dẫn tới file Form.cs hoặc Form.Designer.cs.")]
        string formPath,
        [Description("Danh sách mã rule cần kiểm tra, phân tách bằng dấu phẩy (vd 'WF001,WF040,WF010'). Để trống = chạy tất cả.")]
        string? rules = null,
        [Description("Mức độ nghiêm trọng tối thiểu để báo cáo: 'error', 'warning', hoặc 'info'. Mặc định 'info'.")]
        string minSeverity = "info",
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var result = await Task.Run(() => analyzer.AnalyzeForm(formPath, rules, minSeverity), ct);
            return McpResults.Ok(result);
        });
    }

    [McpServerTool(Name = "wf_analyze_project")]
    [Description("""
        Quét và phân tích tĩnh toàn bộ các Form trong một project WinForms (.csproj) bằng Roslyn.
        Tự động tìm tất cả các file Designer và tổng hợp báo cáo lỗi toàn diện.
        """)]
    public static async Task<CallToolResult> AnalyzeProject(
        FormAnalyzer analyzer,
        [Description("Đường dẫn tới file project .csproj.")]
        string projectPath,
        [Description("Mức độ nghiêm trọng tối thiểu: 'error', 'warning', hoặc 'info'. Mặc định 'warning'.")]
        string minSeverity = "warning",
        [Description("Số lượng Form tối đa quét trong một lần chạy. Mặc định 50.")]
        int maxForms = 50,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var result = await Task.Run(() => analyzer.AnalyzeProject(projectPath, minSeverity, maxForms), ct);
            return McpResults.Ok(result);
        });
    }

    [McpServerTool(Name = "wf_list_rules")]
    [Description("Liệt kê danh sách tất cả các rule phân tích tĩnh (WF001 - WF060) của WinForms Verifier kèm mô tả và hướng dẫn khắc phục.")]
    public static async Task<CallToolResult> ListRules(CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var rules = FormRules.AllRules;
            return await Task.FromResult(McpResults.Ok(new
            {
                totalRules = rules.Count,
                rules = rules
            }));
        });
    }
}
