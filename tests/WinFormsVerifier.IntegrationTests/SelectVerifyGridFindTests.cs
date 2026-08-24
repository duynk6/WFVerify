using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Microsoft.Extensions.Logging.Abstractions;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services;
using Xunit;

namespace WinFormsVerifier.IntegrationTests;

/// <summary>
/// Phủ các sửa đổi sau đợt kiểm thử thực tế trên app QLSX:
/// xác minh hậu điều kiện của wf_select, thông báo lỗi khi index ngoài phạm vi,
/// khớp item chính xác/ambiguous, và wf_grid_find trên DataGridView thật.
/// </summary>
public class SelectVerifyGridFindTests : IDisposable
{
    private readonly UiThread _uiThread = new();
    private readonly UiSession _session;
    private readonly ElementLocator _locator = new();
    private readonly InteractionService _interaction;

    public SelectVerifyGridFindTests()
    {
        _session = new UiSession(_uiThread, NullLogger<UiSession>.Instance);
        _interaction = new InteractionService(_session);
    }

    private static string ResolveSampleExe()
    {
        var exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp", "bin", "Debug", "net8.0-windows", "SampleApp.exe"));
        return File.Exists(exe) ? exe : Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp\bin\Debug\net8.0-windows\SampleApp.exe");
    }

    private async Task<Window> OpenMainAsync()
    {
        var exe = ResolveSampleExe();
        Assert.True(File.Exists(exe), $"SampleApp.exe not found at: {exe}");

        _session.SetSession(Application.Launch(exe), launchedByUs: true);

        var login = await _session.RunAsync(() => _session.ResolveWindow(), TimeSpan.FromSeconds(10));
        await _session.RunAsync(() =>
        {
            _interaction.SetValue(_locator.Resolve(login, "id:txtUsername", TimeSpan.FromSeconds(5)), "admin");
            _interaction.SetValue(_locator.Resolve(login, "id:txtPassword", TimeSpan.FromSeconds(5)), "123456");
            _interaction.Invoke(_locator.Resolve(login, "id:btnLogin", TimeSpan.FromSeconds(5)));
            return true;
        }, TimeSpan.FromSeconds(20));

        return await _session.RunAsync(() =>
        {
            Thread.Sleep(500);
            return _session.ResolveWindow("name~:Quản lý Đơn hàng");
        }, TimeSpan.FromSeconds(10));
    }

    private async Task<Window> OpenCatalogAsync(Window main)
    {
        await _session.RunAsync(() => _interaction.MenuClick(main, "File > Đơn hàng"), TimeSpan.FromSeconds(15));
        return await _session.RunAsync(() =>
        {
            Thread.Sleep(600);
            return _session.ResolveWindow("name~:Danh mục sản phẩm");
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Select_VerifiesPostCondition_AndReportsRangeAndAmbiguityProperly()
    {
        var main = await OpenMainAsync();

        // --- Chọn đúng mục: hậu điều kiện phải khớp nên KHÔNG có warning về selection ---
        var okResult = await _session.RunAsync(() =>
        {
            var cbo = _locator.Resolve(main, "id:cboStatus", TimeSpan.FromSeconds(5));
            return _interaction.Select(cbo, item: "Hoàn thành");
        }, TimeSpan.FromSeconds(20));

        Assert.Contains("Hoàn thành", okResult.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(okResult.Warnings, w => w.Contains("không đổi thành", StringComparison.Ordinal));

        // Ứng dụng thật sự nhận giá trị mới (btnFilter đọc cboStatus.SelectedItem).
        var filterLabel = await _session.RunAsync(() =>
        {
            _interaction.Invoke(_locator.Resolve(main, "id:btnFilter", TimeSpan.FromSeconds(5)));
            Thread.Sleep(300);
            return _locator.Resolve(main, "id:lblFilterResult", TimeSpan.FromSeconds(5)).SafeName();
        }, TimeSpan.FromSeconds(20));

        Assert.Contains("Hoàn thành", filterLabel, StringComparison.Ordinal);

        // --- index ngoài phạm vi: phải báo đúng phạm vi, KHÔNG báo "cần cung cấp item hoặc index" ---
        var rangeEx = await Assert.ThrowsAsync<ToolException>(() => _session.RunAsync(() =>
        {
            var cbo = _locator.Resolve(main, "id:cboStatus", TimeSpan.FromSeconds(5));
            return _interaction.Select(cbo, index: 99);
        }, TimeSpan.FromSeconds(20)));

        Assert.Equal(ErrorCode.ElementNotFound, rangeEx.Code);
        Assert.Contains("nằm ngoài phạm vi", rangeEx.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Cần cung cấp", rangeEx.Message, StringComparison.Ordinal);

        // --- Khớp chứa trúng nhiều mục: AMBIGUOUS thay vì chọn nhầm mục đầu tiên ---
        var catalog = await OpenCatalogAsync(main);
        var ambiguousEx = await Assert.ThrowsAsync<ToolException>(() => _session.RunAsync(() =>
        {
            var list = _locator.Resolve(catalog, "id:lstProducts", TimeSpan.FromSeconds(5));
            return _interaction.Select(list, item: "Sản phẩm 1");
        }, TimeSpan.FromSeconds(20)));

        Assert.Equal(ErrorCode.Ambiguous, ambiguousEx.Code);
        Assert.Contains("Sản phẩm 10", ambiguousEx.Message, StringComparison.Ordinal);

        // --- Khớp chính xác vẫn chọn được dù có mục khác chứa nó ---
        var exact = await _session.RunAsync(() =>
        {
            var list = _locator.Resolve(catalog, "id:lstProducts", TimeSpan.FromSeconds(5));
            var res = _interaction.Select(list, item: "Sản phẩm 10");
            Thread.Sleep(200);
            return (res, label: _locator.Resolve(catalog, "id:lblSelection", TimeSpan.FromSeconds(5)).SafeName());
        }, TimeSpan.FromSeconds(20));

        Assert.Contains("Sản phẩm 10", exact.label, StringComparison.Ordinal);
        Assert.DoesNotContain(exact.res.Warnings, w => w.Contains("không đổi thành", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GridRead_ReportsRealColumnCount_AndGridFind_LocatesRowsByColumn()
    {
        var main = await OpenMainAsync();

        var read = await _session.RunAsync(() =>
        {
            var grid = _locator.Resolve(main, "id:dgOrders", TimeSpan.FromSeconds(5));
            return _interaction.GridRead(grid, startRow: 0, maxRows: 3, maxCols: 10);
        }, TimeSpan.FromSeconds(30));

        var meta = read.Data!.GetType();
        var totalCols = (int)meta.GetProperty("TotalCols")!.GetValue(read.Data)!;
        var totalRows = (int)meta.GetProperty("TotalRows")!.GetValue(read.Data)!;

        Assert.Equal(6, totalCols);   // dgOrders có 6 cột
        Assert.Equal(50, totalRows);  // LoadOrderData() nạp 50 dòng
        Assert.Contains("DH0001", read.Message, StringComparison.Ordinal);

        // --- wf_grid_find theo tên cột ---
        var found = await _session.RunAsync(() =>
        {
            var grid = _locator.Resolve(main, "id:dgOrders", TimeSpan.FromSeconds(5));
            return _interaction.GridFind(grid, column: "Mã ĐH", value: "DH0007", op: "equals");
        }, TimeSpan.FromSeconds(60));

        var foundMeta = found.Data!.GetType();
        var matchCount = (int)foundMeta.GetProperty("MatchCount")!.GetValue(found.Data)!;

        Assert.Equal(1, matchCount);
        Assert.Contains("Row 06", found.Message, StringComparison.Ordinal); // DH0007 nằm ở dòng index 6
        Assert.Contains("Khách hàng 7", found.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadOnlyMode_BlocksSetValue_AndDangerousInvoke_OnRealControls()
    {
        var exe = ResolveSampleExe();
        Assert.True(File.Exists(exe), $"SampleApp.exe not found at: {exe}");
        _session.SetSession(Application.Launch(exe), launchedByUs: true);

        var login = await _session.RunAsync(() => _session.ResolveWindow(), TimeSpan.FromSeconds(10));

        try
        {
            Environment.SetEnvironmentVariable(ReadOnlyGuard.EnabledVariable, "1");
            Environment.SetEnvironmentVariable(ReadOnlyGuard.BlocklistVariable, "Đăng nhập");

            var writeEx = await Assert.ThrowsAsync<ToolException>(() => _session.RunAsync(() =>
            {
                var txt = _locator.Resolve(login, "id:txtUsername", TimeSpan.FromSeconds(5));
                return _interaction.SetValue(txt, "admin");
            }, TimeSpan.FromSeconds(15)));

            Assert.Equal(ErrorCode.ReadOnlyMode, writeEx.Code);

            var invokeEx = await Assert.ThrowsAsync<ToolException>(() => _session.RunAsync(() =>
            {
                var btn = _locator.Resolve(login, "id:btnLogin", TimeSpan.FromSeconds(5));
                return _interaction.Invoke(btn);
            }, TimeSpan.FromSeconds(15)));

            Assert.Equal(ErrorCode.ReadOnlyMode, invokeEx.Code);

            // Ô nhập vẫn rỗng: thao tác bị chặn TRƯỚC khi chạm vào ứng dụng.
            var current = await _session.RunAsync(
                () => _locator.Resolve(login, "id:txtUsername", TimeSpan.FromSeconds(5)).Patterns.Value.Pattern.Value.Value,
                TimeSpan.FromSeconds(10));
            Assert.True(string.IsNullOrEmpty(current), $"Ô username phải còn rỗng nhưng đang là '{current}'.");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ReadOnlyGuard.EnabledVariable, null);
            Environment.SetEnvironmentVariable(ReadOnlyGuard.BlocklistVariable, null);
        }

        // Tắt chế độ chỉ-đọc thì thao tác chạy bình thường trở lại.
        var okValue = await _session.RunAsync(() =>
        {
            var txt = _locator.Resolve(login, "id:txtUsername", TimeSpan.FromSeconds(5));
            _interaction.SetValue(txt, "admin");
            return _locator.Resolve(login, "id:txtUsername", TimeSpan.FromSeconds(5)).Patterns.Value.Pattern.Value.Value;
        }, TimeSpan.FromSeconds(15));

        Assert.Equal("admin", okValue);
    }

    public void Dispose()
    {
        _session.Dispose();
        _uiThread.Dispose();
        GC.SuppressFinalize(this);
    }
}
