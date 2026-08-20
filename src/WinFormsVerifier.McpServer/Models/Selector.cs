namespace WinFormsVerifier.Models;

public class SelectorSegment
{
    public string Raw { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? NameContains { get; set; }
    public string? ControlType { get; set; }
    public string? ClassName { get; set; }
    public string? HelpText { get; set; }
    public int? Index { get; set; }
    public (int Row, int Col)? GridCell { get; set; }

    public static SelectorSegment Parse(string segmentStr)
    {
        var segment = new SelectorSegment { Raw = segmentStr.Trim() };
        var trimmed = segment.Raw;

        // Check for multiple predicates in one segment, e.g. "type:Button > idx:0" or comma/space separated if needed, but standard is prefix:value
        // If no prefix, default to id if starts with '#' or name otherwise
        if (!trimmed.Contains(':'))
        {
            if (trimmed.StartsWith('#'))
            {
                segment.Id = trimmed[1..];
            }
            else
            {
                segment.Name = trimmed;
            }
            return segment;
        }

        var colonIdx = trimmed.IndexOf(':');
        var prefix = trimmed[..colonIdx].Trim().ToLowerInvariant();
        var value = trimmed[(colonIdx + 1)..].Trim();

        switch (prefix)
        {
            case "id":
                segment.Id = value;
                break;
            case "name":
                segment.Name = value;
                break;
            case "name~":
                segment.NameContains = value;
                break;
            case "type":
                segment.ControlType = value;
                break;
            case "class":
                segment.ClassName = value;
                break;
            case "help":
                segment.HelpText = value;
                break;
            case "idx":
                if (int.TryParse(value, out var idx))
                {
                    segment.Index = idx;
                }
                break;
            case "grid":
                var gridParts = value.Split(',', StringSplitOptions.TrimEntries);
                if (gridParts.Length == 2 && int.TryParse(gridParts[0], out var r) && int.TryParse(gridParts[1], out var c))
                {
                    segment.GridCell = (r, c);
                }
                break;
            default:
                segment.Name = value;
                break;
        }

        return segment;
    }
}

public class ParsedSelector
{
    public string Raw { get; }
    public List<SelectorSegment> Segments { get; } = new();

    public ParsedSelector(string raw)
    {
        Raw = raw;
        if (string.IsNullOrWhiteSpace(raw)) return;

        var parts = raw.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            Segments.Add(SelectorSegment.Parse(part));
        }
    }

    public static ParsedSelector Parse(string raw) => new(raw);
}
