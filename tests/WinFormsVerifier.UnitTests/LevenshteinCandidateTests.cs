using WinFormsVerifier.Services;
using Xunit;

namespace WinFormsVerifier.UnitTests;

public class LevenshteinCandidateTests
{
    [Fact]
    public void Similarity_ExactMatch_ReturnsOne()
    {
        var score = ElementLocator.CalculateSimilarity("btnLogin", "btnLogin");
        Assert.Equal(1.0, score);
    }

    [Fact]
    public void Similarity_CloseTypo_ReturnsHighScore()
    {
        var score = ElementLocator.CalculateSimilarity("btnLogn", "btnLogin");
        Assert.True(score >= 0.7, $"Expected score >= 0.7 but got {score}");
    }

    [Fact]
    public void Similarity_CompletelyDifferent_ReturnsLowScore()
    {
        var score = ElementLocator.CalculateSimilarity("txtPassword", "btnSubmit");
        Assert.True(score < 0.4, $"Expected score < 0.4 but got {score}");
    }

    [Fact]
    public void Similarity_SubstringMatch_ReturnsHigh()
    {
        var score = ElementLocator.CalculateSimilarity("Login", "btnLogin");
        Assert.True(score >= 0.8, $"Expected score >= 0.8 but got {score}");
    }
}
