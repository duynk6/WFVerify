using System.Text.Json.Serialization;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WinFormsVerifier.Models;

public class ElementDto
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("className")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClassName { get; set; }

    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; set; }

    [JsonPropertyName("helpText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? HelpText { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("isOffscreen")]
    public bool IsOffscreen { get; set; }

    [JsonPropertyName("bounds")]
    public int[]? Bounds { get; set; } // [x, y, width, height]

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new();

    public static ElementDto FromAutomationElement(AutomationElement element)
    {
        var dto = new ElementDto
        {
            Id = string.IsNullOrEmpty(element.AutomationId) ? null : element.AutomationId,
            Name = string.IsNullOrEmpty(element.Name) ? null : element.Name,
            Type = element.ControlType.ToString(),
            ClassName = string.IsNullOrEmpty(element.ClassName) ? null : element.ClassName,
            HelpText = string.IsNullOrEmpty(element.HelpText) ? null : element.HelpText,
            IsEnabled = element.IsEnabled,
            IsOffscreen = element.IsOffscreen
        };

        try
        {
            var rect = element.BoundingRectangle;
            if (!rect.IsEmpty)
            {
                dto.Bounds = new[] { (int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height };
            }
        }
        catch
        {
            // Ignore if bounds cannot be retrieved
        }

        // Value or text extraction
        try
        {
            if (element.Patterns.Value.IsSupported)
            {
                dto.Value = element.Patterns.Value.Pattern.Value.Value;
            }
            else if (element.Patterns.LegacyIAccessible.IsSupported)
            {
                dto.Value = element.Patterns.LegacyIAccessible.Pattern.Value.Value;
            }
        }
        catch
        {
            // Ignore
        }

        // Detect supported patterns
        dto.Patterns = DetectSupportedPatterns(element);

        return dto;
    }

    public static List<string> DetectSupportedPatterns(AutomationElement element)
    {
        var patterns = new List<string>();
        var p = element.Patterns;

        if (p.Invoke.IsSupported) patterns.Add("Invoke");
        if (p.Value.IsSupported) patterns.Add("Value");
        if (p.Toggle.IsSupported) patterns.Add("Toggle");
        if (p.SelectionItem.IsSupported) patterns.Add("SelectionItem");
        if (p.Selection.IsSupported) patterns.Add("Selection");
        if (p.ExpandCollapse.IsSupported) patterns.Add("ExpandCollapse");
        if (p.Grid.IsSupported) patterns.Add("Grid");
        if (p.GridItem.IsSupported) patterns.Add("GridItem");
        if (p.Table.IsSupported) patterns.Add("Table");
        if (p.Scroll.IsSupported) patterns.Add("Scroll");
        if (p.ScrollItem.IsSupported) patterns.Add("ScrollItem");
        if (p.Transform.IsSupported) patterns.Add("Transform");
        if (p.RangeValue.IsSupported) patterns.Add("RangeValue");
        if (p.LegacyIAccessible.IsSupported) patterns.Add("LegacyIAccessible");

        return patterns;
    }
}
