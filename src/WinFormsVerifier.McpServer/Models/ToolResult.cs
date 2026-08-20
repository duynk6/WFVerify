using System.Text.Json.Serialization;

namespace WinFormsVerifier.Models;

public class ToolResult<T>
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; set; }

    [JsonPropertyName("warnings")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Warnings { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ToolError? Error { get; set; }

    public static ToolResult<T> Success(T? data, IEnumerable<string>? warnings = null)
    {
        var result = new ToolResult<T>
        {
            Ok = true,
            Data = data
        };

        if (warnings != null)
        {
            var list = warnings.Where(w => !string.IsNullOrWhiteSpace(w)).ToList();
            if (list.Count > 0)
            {
                result.Warnings = list;
            }
        }

        return result;
    }

    public static ToolResult<T> Failure(string code, string message, string? hint = null, List<CandidateDto>? candidates = null, object? details = null)
    {
        return new ToolResult<T>
        {
            Ok = false,
            Error = new ToolError
            {
                Code = code,
                Message = message,
                Hint = hint,
                Candidates = candidates,
                Details = details
            }
        };
    }
}

public class ToolError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("hint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Hint { get; set; }

    [JsonPropertyName("candidates")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CandidateDto>? Candidates { get; set; }

    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; set; }
}

public class CandidateDto
{
    [JsonPropertyName("selector")]
    public string Selector { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    [JsonPropertyName("automationId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AutomationId { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }
}
