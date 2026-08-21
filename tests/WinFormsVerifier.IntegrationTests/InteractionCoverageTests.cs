using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Microsoft.Extensions.Logging.Abstractions;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Services;
using Xunit;

namespace WinFormsVerifier.IntegrationTests;

/// <summary>
/// Phủ các tool tương tác trước đây không có fixture nào để chạy thật:
/// toggle, select, expand, send_keys, focus, scroll_into_view.
/// Chạy trên CatalogForm (TreeView, ListBox 60 mục, CheckBox, DateTimePicker, TextBox).
/// </summary>
public class InteractionCoverageTests : IDisposable
{
    private readonly UiThread _uiThread = new();
    private readonly UiSession _session;
    private readonly ElementLocator _locator = new();
    private readonly InteractionService _interaction;

    public InteractionCoverageTests()
    {
        _session = new UiSession(_uiThread, NullLogger<UiSession>.Instance);
        _interaction = new InteractionService(_session);
    }

    private static string ResolveSampleExe()
    {
        var exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp", "bin", "Debug", "net8.0-windows", "SampleApp.exe"));
        return File.Exists(exe) ? exe : Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp\bin\Debug\net8.0-windows\SampleApp.exe");
    }

    private async Task<Window> OpenCatalogAsync()
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

        var main = await _session.RunAsync(() =>
        {
            Thread.Sleep(500);
            return _session.ResolveWindow("name~:Quản lý Đơn hàng");
        }, TimeSpan.FromSeconds(10));

        await _session.RunAsync(() => _interaction.MenuClick(main, "File > Đơn hàng"), TimeSpan.FromSeconds(15));

        return await _session.RunAsync(() =>
        {
            Thread.Sleep(600);
            return _session.ResolveWindow("name~:Danh mục sản phẩm");
        }, TimeSpan.FromSeconds(10));
    }

    private string ReadSelectionLabel(Window catalog)
        => _locator.Resolve(catalog, "id:lblSelection", TimeSpan.FromSeconds(5)).SafeName();

    [Fact]
    public async Task Toggle_Expand_Select_SendKeys_Focus_ScrollIntoView_AllWorkOnRealControls()
    {
        var catalog = await OpenCatalogAsync();
        Assert.NotNull(catalog);

        // --- wf_toggle trên CheckBox ---
        var toggled = await _session.RunAsync(() =>
        {
            var chk = _locator.Resolve(catalog, "id:chkActiveOnly", TimeSpan.FromSeconds(5));
            _interaction.Toggle(chk, "on");
            Thread.Sleep(200);
            return ReadSelectionLabel(catalog);
        }, TimeSpan.FromSeconds(15));
        Assert.Contains("đang hoạt động", toggled, StringComparison.OrdinalIgnoreCase);

        // --- wf_expand trên node TreeView ---
        var expandedChildren = await _session.RunAsync(() =>
        {
            var node = _locator.Resolve(catalog, "name:Điện tử", TimeSpan.FromSeconds(5));
            _interaction.Expand(node, expand: true);
            Thread.Sleep(300);
            return node.FindAllChildren().Length;
        }, TimeSpan.FromSeconds(15));
        Assert.True(expandedChildren >= 3, $"Node 'Điện tử' phải lộ ra 3 node con sau khi expand, thấy {expandedChildren}.");

        // --- wf_select trên node TreeView con ---
        var treeSelected = await _session.RunAsync(() =>
        {
            var child = _locator.Resolve(catalog, "name:Laptop", TimeSpan.FromSeconds(5));
            _interaction.Select(child);
            Thread.Sleep(250);
            return ReadSelectionLabel(catalog);
        }, TimeSpan.FromSeconds(15));
        Assert.Contains("Laptop", treeSelected, StringComparison.Ordinal);

        // --- wf_select theo tên mục trong ListBox ---
        var listSelected = await _session.RunAsync(() =>
        {
            var list = _locator.Resolve(catalog, "id:lstProducts", TimeSpan.FromSeconds(5));
            _interaction.Select(list, item: "Sản phẩm 03");
            Thread.Sleep(250);
            return ReadSelectionLabel(catalog);
        }, TimeSpan.FromSeconds(15));
        Assert.Contains("Sản phẩm 03", listSelected, StringComparison.Ordinal);

        // --- wf_scroll_into_view trên mục nằm ngoài vùng nhìn thấy ---
        var scrolled = await _session.RunAsync(() =>
        {
            var list = _locator.Resolve(catalog, "id:lstProducts", TimeSpan.FromSeconds(5));
            var far = list.FindAllChildren().Last();
            _interaction.ScrollIntoView(far);
            Thread.Sleep(250);
            return far.SafeIsOffscreen();
        }, TimeSpan.FromSeconds(15));
        Assert.False(scrolled, "Mục cuối danh sách phải nằm trong vùng nhìn thấy sau scroll_into_view.");

        // --- wf_focus + wf_send_keys trên TextBox ---
        var typed = await _session.RunAsync(() =>
        {
            var box = _locator.Resolve(catalog, "id:txtSearch", TimeSpan.FromSeconds(5));
            _interaction.Focus(box);
            Thread.Sleep(150);
            var hasFocus = box.SafeHasKeyboardFocus();

            _interaction.SendKeys("laptop", box);
            Thread.Sleep(250);
            var value = box.Patterns.Value.Pattern.Value.Value;
            return (hasFocus, value);
        }, TimeSpan.FromSeconds(15));

        Assert.True(typed.hasFocus, "wf_focus phải đưa được keyboard focus vào TextBox.");
        Assert.Equal("laptop", typed.value);
    }

    public void Dispose()
    {
        _session.Dispose();
        _uiThread.Dispose();
        GC.SuppressFinalize(this);
    }
}
