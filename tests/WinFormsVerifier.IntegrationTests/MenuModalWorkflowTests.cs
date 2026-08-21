using FlaUI.Core;
using Microsoft.Extensions.Logging.Abstractions;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Services;
using Xunit;

namespace WinFormsVerifier.IntegrationTests;

/// <summary>
/// Tái hiện kịch bản: wf_menu_click -> MessageBox bật lên -> wf_dialog_respond.
/// Bug đã báo: dialog_respond trả WINDOW_NOT_FOUND vì dialog đã biến mất.
/// </summary>
public class MenuModalWorkflowTests
{
    private static string ResolveSampleExe()
    {
        var exe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp", "bin", "Debug", "net8.0-windows", "SampleApp.exe"));
        return File.Exists(exe) ? exe : Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp\bin\Debug\net8.0-windows\SampleApp.exe");
    }

    [Fact]
    public async Task MenuClick_OpeningMessageBox_ThenDialogRespond_Succeeds()
    {
        var sampleExe = ResolveSampleExe();
        Assert.True(File.Exists(sampleExe), $"SampleApp.exe not found at: {sampleExe}");

        using var uiThread = new UiThread();
        using var session = new UiSession(uiThread, NullLogger<UiSession>.Instance);
        var locator = new ElementLocator();
        var interaction = new InteractionService(session);

        var app = Application.Launch(sampleExe);
        session.SetSession(app, launchedByUs: true);

        try
        {
            var loginWindow = await session.RunAsync(() => session.ResolveWindow(), TimeSpan.FromSeconds(10));

            // Đăng nhập đúng để mở MainForm (form có MenuStrip).
            await session.RunAsync(() =>
            {
                interaction.SetValue(locator.Resolve(loginWindow, "id:txtUsername", TimeSpan.FromSeconds(5)), "admin");
                interaction.SetValue(locator.Resolve(loginWindow, "id:txtPassword", TimeSpan.FromSeconds(5)), "123456");
                interaction.Invoke(locator.Resolve(loginWindow, "id:btnLogin", TimeSpan.FromSeconds(5)));
                return true;
            }, TimeSpan.FromSeconds(15));

            var mainWindow = await session.RunAsync(() =>
            {
                Thread.Sleep(500);
                return session.ResolveWindow("name~:Quản lý Đơn hàng");
            }, TimeSpan.FromSeconds(10));

            // "Trợ giúp > Giới thiệu" -> menuAbout_Click gọi MessageBox.Show
            var menuResult = await session.RunAsync(
                () => interaction.MenuClick(mainWindow, "Trợ giúp > Giới thiệu"),
                TimeSpan.FromSeconds(15));
            Assert.True(menuResult.Success);

            // MenuClick phải cảnh báo có modal mới xuất hiện.
            Assert.Contains(menuResult.Warnings, w => w.Contains("Modal Dialog", StringComparison.OrdinalIgnoreCase));

            // Và dialog phải vẫn còn đó để dialog_respond xử lý.
            var respond = await session.RunAsync(
                () => interaction.DialogRespond("OK"),
                TimeSpan.FromSeconds(15));
            Assert.True(respond.Success);

            // Sau khi đóng, không còn modal nào.
            var after = await session.RunAsync(() => session.DetectBlockingModal(), TimeSpan.FromSeconds(10));
            Assert.False(after.HasModal, "Modal dialog phải được đóng sau wf_dialog_respond.");
        }
        finally
        {
            session.Dispose();
        }
    }

    /// <summary>
    /// Ô mật khẩu (PasswordChar) bị UIA chặn ValuePattern: SetValue không có tác dụng
    /// nhưng cũng không ném lỗi. Trước khi sửa, tool báo "đã nhập" trong khi ô vẫn rỗng
    /// và đăng nhập thất bại.
    /// </summary>
    [Fact]
    public async Task SetValue_OnPasswordField_ActuallyDeliversTheText()
    {
        var sampleExe = ResolveSampleExe();
        Assert.True(File.Exists(sampleExe), $"SampleApp.exe not found at: {sampleExe}");

        using var uiThread = new UiThread();
        using var session = new UiSession(uiThread, NullLogger<UiSession>.Instance);
        var locator = new ElementLocator();
        var interaction = new InteractionService(session);

        var app = Application.Launch(sampleExe);
        session.SetSession(app, launchedByUs: true);

        try
        {
            var loginWindow = await session.RunAsync(() => session.ResolveWindow(), TimeSpan.FromSeconds(10));

            // Ô mật khẩu phải được nhận diện là password.
            var isPassword = await session.RunAsync(
                () => locator.Resolve(loginWindow, "id:txtPassword", TimeSpan.FromSeconds(5)).SafeIsPassword(),
                TimeSpan.FromSeconds(10));
            Assert.True(isPassword, "txtPassword phải được UIA đánh dấu là ô mật khẩu.");

            var setResult = await session.RunAsync(() =>
            {
                interaction.SetValue(locator.Resolve(loginWindow, "id:txtUsername", TimeSpan.FromSeconds(5)), "admin");
                return interaction.SetValue(locator.Resolve(loginWindow, "id:txtPassword", TimeSpan.FromSeconds(5)), "123456");
            }, TimeSpan.FromSeconds(20));

            // Không được báo "giá trị đọc lại không khớp" cho ô mật khẩu.
            Assert.DoesNotContain(setResult.Warnings, w => w.Contains("không khớp", StringComparison.OrdinalIgnoreCase));

            // Bằng chứng thật sự: đăng nhập phải thành công, tức mật khẩu đã tới được ứng dụng.
            await session.RunAsync(
                () => interaction.Invoke(locator.Resolve(loginWindow, "id:btnLogin", TimeSpan.FromSeconds(5))),
                TimeSpan.FromSeconds(15));

            var noModal = await session.RunAsync(() =>
            {
                Thread.Sleep(400);
                return !session.DetectBlockingModal().HasModal;
            }, TimeSpan.FromSeconds(10));
            Assert.True(noModal, "Đăng nhập lẽ ra phải thành công — còn MessageBox nghĩa là mật khẩu chưa được nhập.");

            var mainWindow = await session.RunAsync(() =>
            {
                Thread.Sleep(300);
                return session.ResolveWindow("name~:Quản lý Đơn hàng");
            }, TimeSpan.FromSeconds(10));
            Assert.NotNull(mainWindow);
        }
        finally
        {
            session.Dispose();
        }
    }
}
