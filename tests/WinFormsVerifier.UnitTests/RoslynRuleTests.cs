using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services.Roslyn;
using Xunit;

namespace WinFormsVerifier.UnitTests;

public class RoslynRuleTests
{
    private readonly FormAnalyzer _analyzer = new();

    [Fact]
    public void AnalyzeForm_BadLayoutForm_CatchesAllViolations()
    {
        var sampleDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp"));
        if (!Directory.Exists(sampleDir))
        {
            sampleDir = Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp");
        }

        var formFile = Path.Combine(sampleDir, "BadLayoutForm.cs");
        Assert.True(File.Exists(formFile), $"Form file does not exist: {formFile}");

        var result = _analyzer.AnalyzeForm(formFile, minSeverity: "info");

        Assert.NotNull(result);
        Assert.Equal("BadLayoutForm", result.Form);

        var ruleIds = result.Diagnostics.Select(d => d.Rule).ToHashSet();

        // Check each violation in BadLayoutForm
        Assert.Contains("WF002", ruleIds); // Orphaned handler
        Assert.Contains("WF010", ruleIds); // Overlapping controls
        Assert.Contains("WF020", ruleIds); // Duplicate TabIndex
        Assert.Contains("WF030", ruleIds); // Dock Fill + Anchor
        Assert.Contains("WF040", ruleIds); // Interactive missing AccessibleName
        Assert.Contains("WF041", ruleIds); // Default name
        Assert.Contains("WF050", ruleIds); // Hardcoded font
        Assert.Contains("WF051", ruleIds); // AutoScaleMode None
        Assert.Contains("WF060", ruleIds); // Dead control
    }

    [Fact]
    public void AnalyzeForm_BrokenHandlerForm_CatchesWF001()
    {
        var fixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        if (!Directory.Exists(fixtureDir))
        {
            fixtureDir = Path.GetFullPath(@"E:\AgentTest\WFVerify\tests\WinFormsVerifier.UnitTests\Fixtures");
        }

        var formFile = Path.Combine(fixtureDir, "BrokenHandlerForm.cs");
        Assert.True(File.Exists(formFile), $"Fixture file does not exist: {formFile}");

        var result = _analyzer.AnalyzeForm(formFile, minSeverity: "error");
        Assert.NotNull(result);
        var ruleIds = result.Diagnostics.Select(d => d.Rule).ToHashSet();
        Assert.Contains("WF001", ruleIds);
    }

    [Fact]
    public void AnalyzeForm_LoginForm_HasZeroErrors()
    {
        var sampleDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp"));
        if (!Directory.Exists(sampleDir))
        {
            sampleDir = Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp");
        }

        var formFile = Path.Combine(sampleDir, "LoginForm.cs");
        Assert.True(File.Exists(formFile), $"Form file does not exist: {formFile}");

        var result = _analyzer.AnalyzeForm(formFile, minSeverity: "error");

        Assert.NotNull(result);
        Assert.Equal(0, result.Summary["error"]);
    }
}
