using WinFormsVerifier.Services;
using Xunit;

namespace WinFormsVerifier.UnitTests;

/// <summary>
/// Regression cho lỗi wf_select chọn nhầm mục: trước đây dùng
/// SafeName().Contains(item) + FirstOrDefault nên "May 1" trúng "May 10",
/// "Tổ 1" trúng "Tổ 11" trong danh mục tiếng Việt.
/// </summary>
public class ItemMatcherTests
{
    private static readonly string[] Vietnamese = { "Tổ 11", "Tổ 1", "Tổ 1TH", "Tổ 2" };

    [Fact]
    public void Match_PrefersExact_OverEarlierContainsMatch()
    {
        var result = ItemMatcher.Match(Vietnamese, "Tổ 1");

        Assert.Equal(ItemMatchKind.Exact, result.Kind);
        Assert.Equal(1, result.Index);
        Assert.Equal("Tổ 1", Vietnamese[result.Index]);
    }

    [Fact]
    public void Match_ContainsHittingSeveralItems_IsAmbiguous_NotFirstOne()
    {
        var result = ItemMatcher.Match(new[] { "May 10", "May 11", "May 12" }, "May 1");

        Assert.Equal(ItemMatchKind.Ambiguous, result.Kind);
        Assert.Equal(-1, result.Index);
        Assert.Equal(new[] { 0, 1, 2 }, result.AmbiguousIndexes);
    }

    [Fact]
    public void Match_SingleContainsHit_IsAccepted()
    {
        var result = ItemMatcher.Match(new[] { "Chờ xử lý", "Đang giao", "Hoàn thành" }, "giao");

        Assert.Equal(ItemMatchKind.Contains, result.Kind);
        Assert.Equal(1, result.Index);
    }

    [Fact]
    public void Match_ExactIsCaseInsensitiveAndTrimmed()
    {
        var result = ItemMatcher.Match(new[] { " Hoàn thành ", "Khác" }, "hoàn thành");

        Assert.Equal(ItemMatchKind.Exact, result.Kind);
        Assert.Equal(0, result.Index);
    }

    [Fact]
    public void Match_NoHit_ReturnsNotFound()
    {
        var result = ItemMatcher.Match(Vietnamese, "Tổ 9");

        Assert.Equal(ItemMatchKind.NotFound, result.Kind);
    }

    [Fact]
    public void Match_EmptyList_ReturnsNotFound()
    {
        Assert.Equal(ItemMatchKind.NotFound, ItemMatcher.Match(Array.Empty<string>(), "gì đó").Kind);
    }
}
