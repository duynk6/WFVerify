using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Infrastructure;

public static class McpResults
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static CallToolResult Ok(object? data = null, IEnumerable<string>? warnings = null)
    {
        var envelope = ToolResult<object>.Success(data, warnings);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        return new CallToolResult
        {
            IsError = false,
            Content = { new TextContentBlock { Text = json } }
        };
    }

    public static CallToolResult Fail(
        string code,
        string message,
        string? hint = null,
        List<CandidateDto>? candidates = null,
        object? details = null)
    {
        var envelope = ToolResult<object>.Failure(code, message, hint, candidates, details);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        return new CallToolResult
        {
            IsError = true,
            Content = { new TextContentBlock { Text = json } }
        };
    }

    public static CallToolResult BlockedByModal(
        string dialogTitle,
        string dialogText,
        IEnumerable<string>? buttons = null)
    {
        var buttonList = buttons?.ToList() ?? new List<string>();
        var buttonStr = buttonList.Count > 0 ? string.Join(", ", buttonList) : "OK / Cancel";
        return Fail(
            ErrorCode.BlockedByModal,
            $"Thao tác bị chặn bởi modal dialog: \"{dialogTitle}\" ({dialogText}).",
            $"Hãy dùng tool 'wf_dialog_respond' với nút [{buttonStr}] hoặc 'wf_close_app' để xử lý dialog trước khi tiếp tục.",
            details: new
            {
                DialogTitle = dialogTitle,
                DialogText = dialogText,
                Buttons = buttonList
            });
    }

    public static async Task<CallToolResult> GuardAsync(Func<Task<CallToolResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ToolException ex)
        {
            return Fail(ex.Code, ex.Message, ex.Hint, ex.Candidates, ex.Details);
        }
        catch (OperationCanceledException)
        {
            return Fail(
                ErrorCode.Timeout,
                "Thao tác đã bị hủy hoặc vượt quá thời gian chờ (Timeout).",
                "Có thể ứng dụng đang bận hoặc bị treo. Hãy kiểm tra trạng thái hoặc gọi wf_wait_idle.");
        }
        catch (Exception ex)
        {
            return Fail(
                ErrorCode.Internal,
                $"Lỗi nội bộ server ({ex.GetType().Name}): {ex.Message}",
                "Vui lòng kiểm tra log server (stderr) hoặc thử lại thao tác.");
        }
    }
}
