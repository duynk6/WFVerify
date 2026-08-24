using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Tools;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Services;

public sealed class ElementLocator
{
    public AutomationElement Resolve(AutomationElement scope, string selector, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return scope;
        }

        var parsed = ParsedSelector.Parse(selector);
        if (parsed.Segments.Count == 0)
        {
            return scope;
        }

        var current = scope;
        for (int i = 0; i < parsed.Segments.Count; i++)
        {
            var segment = parsed.Segments[i];
            var isLast = i == parsed.Segments.Count - 1;

            var match = Retry.WhileNull(
                () => FindSegment(current, segment),
                timeout,
                TimeSpan.FromMilliseconds(100)).Result;

            if (match == null)
            {
                // If it's the last segment and we couldn't find it, suggest candidates
                var candidates = SuggestCandidates(scope, selector, 10);
                var hint = candidates.Count > 0
                    ? $"Có phải bạn muốn: '{candidates[0].Selector}' (score: {candidates[0].Score})?"
                    : "Hãy dùng tool 'wf_get_ui_tree' để kiểm tra danh sách control thực tế.";

                throw new ToolException(
                    ErrorCode.ElementNotFound,
                    $"Không tìm thấy element nào khớp với selector '{selector}' (thất bại ở segment '{segment.Raw}').",
                    hint,
                    candidates);
            }

            current = match;
        }

        return current;
    }

    public IReadOnlyList<AutomationElement> ResolveAll(AutomationElement scope, string selector, int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            return new[] { scope };
        }

        var parsed = ParsedSelector.Parse(selector);
        if (parsed.Segments.Count == 0)
        {
            return new[] { scope };
        }

        var currentScopes = new List<AutomationElement> { scope };
        for (int i = 0; i < parsed.Segments.Count; i++)
        {
            var segment = parsed.Segments[i];
            var nextScopes = new List<AutomationElement>();

            foreach (var s in currentScopes)
            {
                var matches = FindAllSegmentMatches(s, segment, limit);
                nextScopes.AddRange(matches);
                if (nextScopes.Count >= limit) break;
            }

            currentScopes = nextScopes.DistinctBy(IdentityKey).Take(limit).ToList();
            if (currentScopes.Count == 0) break;
        }

        return currentScopes;
    }

    private AutomationElement? FindSegment(AutomationElement scope, SelectorSegment segment)
    {
        // 1. Grid cell resolution
        if (segment.GridCell.HasValue)
        {
            var (row, col) = segment.GridCell.Value;
            if (scope.Patterns.Grid.IsSupported)
            {
                var item = scope.Patterns.Grid.Pattern.GetItem(row, col);
                if (item != null) return item;
            }
        }

        // 2. Exact AutomationId
        if (!string.IsNullOrEmpty(segment.Id))
        {
            var byId = FirstPreferringVisible(scope.FindAllDescendants(cf => cf.ByAutomationId(segment.Id)));
            if (byId != null) return byId;
        }

        // 3. Exact Name
        if (!string.IsNullOrEmpty(segment.Name))
        {
            var byName = FirstPreferringVisible(scope.FindAllDescendants(cf => cf.ByName(segment.Name)));
            if (byName != null) return byName;
        }

        // 4. Name contains
        if (!string.IsNullOrEmpty(segment.NameContains))
        {
            var all = scope.FindAllDescendants();
            var matched = all.FirstOrDefault(e => e.SafeName().Contains(segment.NameContains, StringComparison.OrdinalIgnoreCase));
            if (matched != null) return matched;
        }

        // 5. ControlType
        if (!string.IsNullOrEmpty(segment.ControlType))
        {
            if (Enum.TryParse<ControlType>(segment.ControlType, true, out var ct))
            {
                if (segment.Index.HasValue)
                {
                    var allOfType = scope.FindAllDescendants(cf => cf.ByControlType(ct));
                    if (segment.Index.Value >= 0 && segment.Index.Value < allOfType.Length)
                    {
                        return allOfType[segment.Index.Value];
                    }
                }
                else
                {
                    var byType = scope.FindFirstDescendant(cf => cf.ByControlType(ct));
                    if (byType != null) return byType;
                }
            }
        }

        // 6. ClassName
        if (!string.IsNullOrEmpty(segment.ClassName))
        {
            var byClass = scope.FindFirstDescendant(cf => cf.ByClassName(segment.ClassName));
            if (byClass != null) return byClass;
        }

        // 7. HelpText
        if (!string.IsNullOrEmpty(segment.HelpText))
        {
            var byHelp = scope.FindFirstDescendant(cf => cf.ByHelpText(segment.HelpText));
            if (byHelp != null) return byHelp;
        }

        // 8. Raw index among children
        if (segment.Index.HasValue)
        {
            var children = scope.FindAllChildren();
            if (segment.Index.Value >= 0 && segment.Index.Value < children.Length)
            {
                return children[segment.Index.Value];
            }
        }

        return null;
    }

    /// <summary>
    /// Nhiều control trong cùng form có thể trùng AutomationId/Name (4 tab của một form dùng chung
    /// tên 'fg', 'txtMaHang'...). Ưu tiên control đang HIỂN THỊ để 'id:fg' không trả về grid của tab
    /// đang ẩn. Muốn nhắm đúng tab cụ thể thì dùng selector phân cấp: 'id:tabTheoDoi > id:fg'.
    /// </summary>
    private static AutomationElement? FirstPreferringVisible(AutomationElement[] matches)
    {
        if (matches.Length == 0) return null;
        if (matches.Length == 1) return matches[0];

        foreach (var match in matches)
        {
            if (!match.SafeIsOffscreen()) return match;
        }

        return matches[0];
    }

    private List<AutomationElement> FindAllSegmentMatches(AutomationElement scope, SelectorSegment segment, int limit)
    {
        var results = new List<AutomationElement>();

        if (!string.IsNullOrEmpty(segment.Id))
        {
            results.AddRange(scope.FindAllDescendants(cf => cf.ByAutomationId(segment.Id)));
        }
        else if (!string.IsNullOrEmpty(segment.Name))
        {
            results.AddRange(scope.FindAllDescendants(cf => cf.ByName(segment.Name)));
        }
        else if (!string.IsNullOrEmpty(segment.NameContains))
        {
            var all = scope.FindAllDescendants();
            results.AddRange(all.Where(e => e.SafeName().Contains(segment.NameContains, StringComparison.OrdinalIgnoreCase)));
        }
        else if (!string.IsNullOrEmpty(segment.ControlType) && Enum.TryParse<ControlType>(segment.ControlType, true, out var ct))
        {
            results.AddRange(scope.FindAllDescendants(cf => cf.ByControlType(ct)));
        }
        else if (!string.IsNullOrEmpty(segment.ClassName))
        {
            results.AddRange(scope.FindAllDescendants(cf => cf.ByClassName(segment.ClassName)));
        }
        else if (!string.IsNullOrEmpty(segment.HelpText))
        {
            results.AddRange(scope.FindAllDescendants(cf => cf.ByHelpText(segment.HelpText)));
        }
        else
        {
            results.AddRange(scope.FindAllChildren());
        }

        return results.Take(limit).ToList();
    }

    public List<CandidateDto> SuggestCandidates(AutomationElement scope, string targetQuery, int take = 10)
    {
        var candidates = new List<CandidateDto>();

        try
        {
            var allElements = scope.FindAllDescendants();
            var query = targetQuery.Contains(':') ? targetQuery.Split(':')[1].Trim() : targetQuery.Trim();

            foreach (var el in allElements)
            {
                // Guard per element: một element lỗi property không được làm hỏng cả danh sách gợi ý
                string id, name, type;
                try
                {
                    if (el.SafeIsOffscreen()) continue;
                    id = el.SafeAutomationId();
                    name = el.SafeName();
                    type = el.SafeControlTypeName();
                }
                catch
                {
                    continue;
                }

                double scoreId = !string.IsNullOrEmpty(id) ? CalculateSimilarity(query, id) : 0;
                double scoreName = !string.IsNullOrEmpty(name) ? CalculateSimilarity(query, name) : 0;
                double maxScore = Math.Max(scoreId, scoreName);

                if (maxScore > 0.3)
                {
                    string bestSelector = !string.IsNullOrEmpty(id) ? $"id:{id}" : $"name:{name}";
                    candidates.Add(new CandidateDto
                    {
                        Selector = bestSelector,
                        Name = name,
                        Type = type,
                        AutomationId = id,
                        Score = maxScore
                    });
                }
            }
        }
        catch
        {
            // Ignore candidate suggestion errors
        }

        return candidates
            .OrderByDescending(c => c.Score)
            .DistinctBy(c => c.Selector)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// Khóa định danh để loại trùng. Không dùng riêng NativeWindowHandle vì các control
    /// không có HWND riêng (ToolStrip item, DataGridView cell) đều trả về cùng một handle
    /// của container -> DistinctBy sẽ gộp nhầm các element khác nhau thành một.
    /// </summary>
    private static string IdentityKey(AutomationElement e)
    {
        try
        {
            var runtimeId = e.Properties.RuntimeId.ValueOrDefault;
            if (runtimeId is { Length: > 0 })
            {
                return string.Join(".", runtimeId);
            }
        }
        catch
        {
            // rơi xuống khóa tổng hợp bên dưới
        }

        var rect = e.SafeBoundingRectangle();
        return $"{e.SafeNativeWindowHandle()}|{e.SafeAutomationId()}|{e.SafeName()}|{e.SafeControlTypeName()}|{rect.X},{rect.Y},{rect.Width},{rect.Height}";
    }

    public static double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0;
        source = source.ToLowerInvariant();
        target = target.ToLowerInvariant();

        if (source == target) return 1.0;
        if (source.Contains(target) || target.Contains(source)) return 0.85;

        int distance = LevenshteinDistance(source, target);
        int maxLen = Math.Max(source.Length, target.Length);
        if (maxLen == 0) return 1.0;

        return Math.Round(Math.Max(0, 1.0 - (double)distance / maxLen), 2);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
