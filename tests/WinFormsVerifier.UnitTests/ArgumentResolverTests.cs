using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using Xunit;

namespace WinFormsVerifier.UnitTests;

/// <summary>
/// wf_launch_app phải nhận được credential mà không cần viết mật khẩu vào lời gọi tool.
/// </summary>
public class ArgumentResolverTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wfverify-args-" + Guid.NewGuid().ToString("N"));

    public ArgumentResolverTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("WFVERIFY_TEST_PWD", null);
        try { Directory.Delete(_dir, recursive: true); } catch { /* ignore */ }
        GC.SuppressFinalize(this);
    }

    // Bỏ qua whitelist của PathGuard: bài test chỉ kiểm tra phần giải placeholder.
    private static string Passthrough(string p) => Path.GetFullPath(p);

    [Fact]
    public void Resolve_EnvPlaceholder_ReturnsEnvironmentValue()
    {
        Environment.SetEnvironmentVariable("WFVERIFY_TEST_PWD", "s3cr3t");

        Assert.Equal("/pwd:s3cr3t", ArgumentResolver.Resolve("/pwd:${env:WFVERIFY_TEST_PWD}"));
    }

    [Fact]
    public void Resolve_MissingEnv_ThrowsWithHint()
    {
        var ex = Assert.Throws<ToolException>(() => ArgumentResolver.Resolve("${env:WFVERIFY_KHONG_TON_TAI}"));
        Assert.Equal(ErrorCode.Internal, ex.Code);
        Assert.Contains("WFVERIFY_KHONG_TON_TAI", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_FilePlaceholderWithJsonKeyPath_ReturnsNestedValue()
    {
        var file = Path.Combine(_dir, "creds.json");
        File.WriteAllText(file, """{ "sql": { "user": "sa", "password": "P@ss word" } }""");

        var resolved = ArgumentResolver.Resolve($"-p=${{file:{file}#sql.password}}", Passthrough);

        Assert.Equal("-p=P@ss word", resolved);
    }

    [Fact]
    public void Resolve_FilePlaceholderWithoutKey_ReturnsTrimmedContent()
    {
        var file = Path.Combine(_dir, "token.txt");
        File.WriteAllText(file, "  abc123\r\n");

        Assert.Equal("abc123", ArgumentResolver.Resolve($"${{file:{file}}}", Passthrough));
    }

    [Fact]
    public void Resolve_MissingJsonKey_ThrowsNamingTheKey()
    {
        var file = Path.Combine(_dir, "creds.json");
        File.WriteAllText(file, """{ "sql": { "user": "sa" } }""");

        var ex = Assert.Throws<ToolException>(() => ArgumentResolver.Resolve($"${{file:{file}#sql.password}}", Passthrough));
        Assert.Contains("password", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_LeavesPlainArgumentUntouched()
    {
        Assert.Equal("--env=UAT", ArgumentResolver.Resolve("--env=UAT"));
    }

    [Fact]
    public void ParseArgumentsFile_JsonArray()
    {
        var args = ArgumentResolver.ParseArgumentsFile("""["-u","sa","-p","x y"]""");
        Assert.Equal(new[] { "-u", "sa", "-p", "x y" }, args);
    }

    [Fact]
    public void ParseArgumentsFile_TextLines_SkipsCommentsAndBlanks()
    {
        var args = ArgumentResolver.ParseArgumentsFile("# chú thích\r\n-u\r\nsa\r\n\r\n-p\r\nsecret\r\n");
        Assert.Equal(new[] { "-u", "sa", "-p", "secret" }, args);
    }
}
