using System.Text.Json.Serialization;

namespace WinFormsVerifier.Models;

public class DiagnosticItem
{
    [JsonPropertyName("rule")]
    public string Rule { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "warning"; // error, warning, info

    [JsonPropertyName("control")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Control { get; set; }

    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int Line { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("fix")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Fix { get; set; }
}

public class FormAnalysisResult
{
    [JsonPropertyName("form")]
    public string Form { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();

    [JsonPropertyName("controlCount")]
    public int ControlCount { get; set; }

    [JsonPropertyName("diagnostics")]
    public List<DiagnosticItem> Diagnostics { get; set; } = new();

    [JsonPropertyName("summary")]
    public Dictionary<string, int> Summary { get; set; } = new();
}

public class ProjectAnalysisResult
{
    [JsonPropertyName("project")]
    public string Project { get; set; } = string.Empty;

    [JsonPropertyName("formsAnalyzed")]
    public int FormsAnalyzed { get; set; }

    [JsonPropertyName("forms")]
    public List<FormAnalysisResult> Forms { get; set; } = new();

    [JsonPropertyName("summary")]
    public Dictionary<string, int> Summary { get; set; } = new();
}

public class RuleInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("fixGuidance")]
    public string FixGuidance { get; set; } = string.Empty;
}
