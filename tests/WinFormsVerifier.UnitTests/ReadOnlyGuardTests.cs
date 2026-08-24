using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using Xunit;

namespace WinFormsVerifier.UnitTests;

public class ReadOnlyGuardTests : IDisposable
{
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ReadOnlyGuard.EnabledVariable, null);
        Environment.SetEnvironmentVariable(ReadOnlyGuard.BlocklistVariable, null);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("Ghi")]
    [InlineData("Lưu dữ liệu")]
    [InlineData("Cập nhật đơn hàng")]
    [InlineData("Xóa dòng")]
    [InlineData("Duyệt")]
    public void MatchBlockedKeyword_CatchesWriteButtons(string label)
    {
        Assert.NotNull(ReadOnlyGuard.MatchBlockedKeyword(label, ReadOnlyGuard.DefaultBlocklist));
    }

    [Theory]
    [InlineData("Nghiên cứu")]      // chứa "ghi" nhưng không phải từ riêng
    [InlineData("Xem báo cáo")]
    [InlineData("Lưuý")]            // "Lưu" dính liền ký tự khác
    [InlineData("Đăng nhập")]
    public void MatchBlockedKeyword_DoesNotCatchHarmlessLabels(string label)
    {
        Assert.Null(ReadOnlyGuard.MatchBlockedKeyword(label, ReadOnlyGuard.DefaultBlocklist));
    }

    [Fact]
    public void EnsureWriteAllowed_IsNoOp_WhenDisabled()
    {
        Environment.SetEnvironmentVariable(ReadOnlyGuard.EnabledVariable, null);
        ReadOnlyGuard.EnsureWriteAllowed("wf_set_value", "txtGhiChu");
    }

    [Fact]
    public void EnsureWriteAllowed_Throws_ReadOnlyMode_WhenEnabled()
    {
        Environment.SetEnvironmentVariable(ReadOnlyGuard.EnabledVariable, "1");

        var ex = Assert.Throws<ToolException>(() => ReadOnlyGuard.EnsureWriteAllowed("wf_set_value", "txtGhiChu"));
        Assert.Equal(ErrorCode.ReadOnlyMode, ex.Code);
    }

    [Fact]
    public void EnsureInvokeAllowed_BlocksDangerousButton_ButAllowsSafeOne()
    {
        Environment.SetEnvironmentVariable(ReadOnlyGuard.EnabledVariable, "true");

        var ex = Assert.Throws<ToolException>(() => ReadOnlyGuard.EnsureInvokeAllowed("Ghi"));
        Assert.Equal(ErrorCode.ReadOnlyMode, ex.Code);

        ReadOnlyGuard.EnsureInvokeAllowed("Tìm kiếm");
    }

    [Fact]
    public void Blocklist_CanBeOverriddenByEnvironment()
    {
        Environment.SetEnvironmentVariable(ReadOnlyGuard.EnabledVariable, "on");
        Environment.SetEnvironmentVariable(ReadOnlyGuard.BlocklistVariable, "Kết chuyển;Khóa sổ");

        Assert.Throws<ToolException>(() => ReadOnlyGuard.EnsureInvokeAllowed("Kết chuyển cuối kỳ"));
        ReadOnlyGuard.EnsureInvokeAllowed("Ghi"); // không còn nằm trong danh sách tuỳ biến
    }
}
