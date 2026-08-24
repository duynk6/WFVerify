namespace WinFormsVerifier.Services;

public enum ItemMatchKind
{
    NotFound,
    Exact,
    Contains,
    Ambiguous
}

public sealed record ItemMatchResult(ItemMatchKind Kind, int Index, IReadOnlyList<int> AmbiguousIndexes)
{
    public static ItemMatchResult NotFound() => new(ItemMatchKind.NotFound, -1, Array.Empty<int>());
}

/// <summary>
/// Khớp tên item trong danh sách (ComboBox/ListBox/Tab).
/// Ưu tiên khớp CHÍNH XÁC trước khi khớp chứa: dùng Contains + FirstOrDefault như trước
/// làm "May 1" chọn nhầm "May 10", hay "Tổ 1" chọn nhầm "Tổ 11" trong danh mục tiếng Việt.
/// Nếu khớp-chứa trúng nhiều mục thì báo AMBIGUOUS thay vì âm thầm lấy mục đầu tiên.
/// </summary>
public static class ItemMatcher
{
    public static ItemMatchResult Match(IReadOnlyList<string> names, string item)
    {
        if (names.Count == 0 || string.IsNullOrEmpty(item)) return ItemMatchResult.NotFound();

        var needle = item.Trim();

        var exact = new List<int>();
        for (int i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i]?.Trim(), needle, StringComparison.OrdinalIgnoreCase))
            {
                exact.Add(i);
            }
        }

        // Trùng tên tuyệt đối thì mục đầu tiên là lựa chọn hợp lý duy nhất — không có gì để phân biệt.
        if (exact.Count > 0) return new ItemMatchResult(ItemMatchKind.Exact, exact[0], exact);

        var contains = new List<int>();
        for (int i = 0; i < names.Count; i++)
        {
            if (names[i]?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true)
            {
                contains.Add(i);
            }
        }

        if (contains.Count == 1) return new ItemMatchResult(ItemMatchKind.Contains, contains[0], contains);
        if (contains.Count > 1) return new ItemMatchResult(ItemMatchKind.Ambiguous, -1, contains);

        return ItemMatchResult.NotFound();
    }
}
