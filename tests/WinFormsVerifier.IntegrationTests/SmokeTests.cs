using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Services;
using WinFormsVerifier.Services.Roslyn;
using Xunit;

namespace WinFormsVerifier.IntegrationTests;

public class SmokeTests
{
    [Fact]
    public void McpResults_Ok_SerializesProperly()
    {
        var result = McpResults.Ok(new { test = 123 }, new[] { "Warning 1" });
        Assert.False(result.IsError);
        Assert.Single(result.Content);
    }

    [Fact]
    public void McpResults_Fail_SetsIsErrorTrue()
    {
        var result = McpResults.Fail("TEST_ERROR", "Test message", "Test hint");
        Assert.True(result.IsError);
        Assert.Single(result.Content);
    }

    [Fact]
    public void FormAnalyzer_CanAnalyzeSampleProject()
    {
        var analyzer = new FormAnalyzer();
        var sampleDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp"));
        if (!Directory.Exists(sampleDir))
        {
            sampleDir = Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp");
        }

        var projFile = Path.Combine(sampleDir, "SampleApp.csproj");
        Assert.True(File.Exists(projFile), $"SampleApp.csproj does not exist: {projFile}");

        var result = analyzer.AnalyzeProject(projFile, minSeverity: "info");
        Assert.NotNull(result);
        Assert.True(result.FormsAnalyzed >= 3, $"Expected at least 3 forms analyzed, got {result.FormsAnalyzed}");
    }
}
