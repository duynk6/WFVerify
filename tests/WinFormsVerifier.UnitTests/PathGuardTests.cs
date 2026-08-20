using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using Xunit;

namespace WinFormsVerifier.UnitTests;

public class PathGuardTests
{
    [Fact]
    public void ValidateAndNormalize_CurrentDirectory_Succeeds()
    {
        var current = Environment.CurrentDirectory;
        var normalized = PathGuard.ValidateAndNormalize(current);
        Assert.NotNull(normalized);
    }

    [Fact]
    public void ValidateAndNormalize_DisallowedPath_ThrowsToolException()
    {
        var disallowed = @"C:\Windows\System32\cmd.exe";
        var ex = Assert.Throws<ToolException>(() => PathGuard.ValidateAndNormalize(disallowed));
        Assert.Equal(ErrorCode.PathDenied, ex.Code);
    }
}
