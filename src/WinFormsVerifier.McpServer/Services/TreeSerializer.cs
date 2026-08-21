using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using WinFormsVerifier.Infrastructure;

namespace WinFormsVerifier.Services;

public sealed class TreeSerializer
{
    public (string Text, List<string> Warnings) Serialize(
        AutomationElement root,
        int maxDepth = 5,
        string? filterTypes = null,
        int maxNodes = 300,
        bool includeInvisible = false)
    {
        var sb = new StringBuilder();
        var warnings = new List<string>();
        int totalNodes = 0;
        int skippedNodes = 0;

        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(filterTypes))
        {
            foreach (var t in filterTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                allowedTypes.Add(t);
            }
        }

        var automation = (UIA3Automation)root.Automation;
        var treeWalker = automation.TreeWalkerFactory.GetControlViewWalker();

        void Walk(AutomationElement element, int currentDepth)
        {
            if (totalNodes >= maxNodes)
            {
                skippedNodes++;
                return;
            }

            if (!includeInvisible && element.SafeIsOffscreen())
            {
                return;
            }

            var typeStr = element.SafeControlTypeName();
            var shouldDisplay = allowedTypes.Count == 0 || allowedTypes.Contains(typeStr);

            if (shouldDisplay)
            {
                totalNodes++;
                AppendElementLine(sb, element, currentDepth);
            }

            if (currentDepth >= maxDepth)
            {
                // Count any immediate children as truncated if we reached maxDepth
                var firstChild = treeWalker.GetFirstChild(element);
                if (firstChild != null)
                {
                    skippedNodes++;
                }
                return;
            }

            var child = treeWalker.GetFirstChild(element);
            while (child != null)
            {
                Walk(child, currentDepth + 1);
                child = treeWalker.GetNextSibling(child);
            }
        }

        Walk(root, 0);

        if (totalNodes >= maxNodes || skippedNodes > 0)
        {
            sb.AppendLine($"  ... (còn khoảng {skippedNodes} control chưa hiển thị — tăng maxDepth hoặc dùng wf_find_elements)");
            warnings.Add($"Cây UI bị giới hạn ở {totalNodes} nodes / độ sâu {maxDepth}. Còn {skippedNodes} elements chưa hiển thị.");
        }

        return (sb.ToString().TrimEnd(), warnings);
    }

    private static void AppendElementLine(StringBuilder sb, AutomationElement element, int depth)
    {
        var indent = new string(' ', depth * 2);
        var type = element.SafeControlTypeName();
        var id = element.SafeAutomationId();
        var name = element.SafeName();

        sb.Append(indent);
        sb.Append(type.PadRight(12));

        if (!string.IsNullOrWhiteSpace(id))
        {
            sb.Append($" id={id}");
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            sb.Append($" name=\"{EscapeString(name)}\"");
        }

        try
        {
            if (element.Patterns.Value.IsSupported)
            {
                var val = element.Patterns.Value.Pattern.Value.Value;
                if (!string.IsNullOrWhiteSpace(val))
                {
                    sb.Append($" val=\"{EscapeString(val)}\"");
                }
            }
        }
        catch
        {
            // Ignore value reading errors
        }

        // Check if disabled
        if (!element.SafeIsEnabled())
        {
            sb.Append(" DISABLED");
        }

        // Bounds info
        try
        {
            var rect = element.SafeBoundingRectangle();
            if (!rect.IsEmpty)
            {
                sb.Append($" @{(int)rect.X},{(int)rect.Y} {(int)rect.Width}x{(int)rect.Height}");
            }
        }
        catch
        {
            // Ignore
        }

        // Special control info (e.g. DataGridView rows/cols)
        try
        {
            if (element.Patterns.Grid.IsSupported)
            {
                var grid = element.Patterns.Grid.Pattern;
                sb.Append($" rows={grid.RowCount.Value} cols={grid.ColumnCount.Value}");
            }
        }
        catch
        {
            // Ignore
        }

        sb.AppendLine();
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\r", "").Replace("\n", " ").Replace("\"", "\\\"");
    }
}
