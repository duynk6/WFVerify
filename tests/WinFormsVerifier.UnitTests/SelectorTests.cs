using WinFormsVerifier.Models;
using Xunit;

namespace WinFormsVerifier.UnitTests;

public class SelectorTests
{
    [Fact]
    public void Parse_SingleSegment_Id()
    {
        var parsed = ParsedSelector.Parse("id:txtUsername");
        Assert.Single(parsed.Segments);
        Assert.Equal("txtUsername", parsed.Segments[0].Id);
    }

    [Fact]
    public void Parse_SingleSegment_NameContains()
    {
        var parsed = ParsedSelector.Parse("name~:Đăng");
        Assert.Single(parsed.Segments);
        Assert.Equal("Đăng", parsed.Segments[0].NameContains);
    }

    [Fact]
    public void Parse_Hierarchical_Selector()
    {
        var parsed = ParsedSelector.Parse("type:Menu > name:File > name:Thoát");
        Assert.Equal(3, parsed.Segments.Count);
        Assert.Equal("Menu", parsed.Segments[0].ControlType);
        Assert.Equal("File", parsed.Segments[1].Name);
        Assert.Equal("Thoát", parsed.Segments[2].Name);
    }

    [Fact]
    public void Parse_GridCoordinates()
    {
        var parsed = ParsedSelector.Parse("id:dgOrders > grid:2,5");
        Assert.Equal(2, parsed.Segments.Count);
        Assert.Equal("dgOrders", parsed.Segments[0].Id);
        Assert.NotNull(parsed.Segments[1].GridCell);
        Assert.Equal((2, 5), parsed.Segments[1].GridCell!.Value);
    }

    [Fact]
    public void Parse_IndexSegment()
    {
        var parsed = ParsedSelector.Parse("type:Button > idx:3");
        Assert.Equal(2, parsed.Segments.Count);
        Assert.Equal("Button", parsed.Segments[0].ControlType);
        Assert.Equal(3, parsed.Segments[1].Index);
    }
}
