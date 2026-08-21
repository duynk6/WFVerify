using System.Diagnostics;
using FlaUI.Core;
using Microsoft.Extensions.Logging.Abstractions;
using WinFormsVerifier.Models;
using WinFormsVerifier.Services;
using Xunit;

namespace WinFormsVerifier.IntegrationTests;

public class LiveUiWorkflowTests
{
    [Fact]
    public async Task FullInteractiveWorkflow_SampleApp_SucceedsEndToEnd()
    {
        var sampleExe = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "SampleApp", "bin", "Debug", "net8.0-windows", "SampleApp.exe"));
        if (!File.Exists(sampleExe))
        {
            sampleExe = Path.GetFullPath(@"E:\AgentTest\WFVerify\samples\SampleApp\bin\Debug\net8.0-windows\SampleApp.exe");
        }

        Assert.True(File.Exists(sampleExe), $"SampleApp.exe not found at: {sampleExe}. Make sure SampleApp is built.");

        using var uiThread = new UiThread();
        using var session = new UiSession(uiThread, NullLogger<UiSession>.Instance);
        var locator = new ElementLocator();
        var interaction = new InteractionService(session);
        var serializer = new TreeSerializer();
        var screenshotService = new ScreenshotService();

        // 1. Launch SampleApp
        var app = Application.Launch(sampleExe);
        session.SetSession(app, launchedByUs: true);

        try
        {
            // 2. Resolve LoginForm
            var loginWindow = await session.RunAsync(() => session.ResolveWindow(), TimeSpan.FromSeconds(10));
            Assert.NotNull(loginWindow);

            // 3. Serialize UI Tree
            var (treeText, warnings) = await session.RunAsync(() => serializer.Serialize(loginWindow, 5), TimeSpan.FromSeconds(10));
            Assert.Contains("txtUsername", treeText);
            Assert.Contains("btnLogin", treeText);

            // 4. Test Wrong Login -> Trigger Modal Dialog
            var txtUser = await session.RunAsync(() => locator.Resolve(loginWindow, "id:txtUsername", TimeSpan.FromSeconds(3)), TimeSpan.FromSeconds(5));
            await session.RunAsync(() => interaction.SetValue(txtUser, "wrong_user"), TimeSpan.FromSeconds(5));

            var txtPass = await session.RunAsync(() => locator.Resolve(loginWindow, "id:txtPassword", TimeSpan.FromSeconds(3)), TimeSpan.FromSeconds(5));
            await session.RunAsync(() => interaction.SetValue(txtPass, "wrong_pass"), TimeSpan.FromSeconds(5));

            var btnLogin = await session.RunAsync(() => locator.Resolve(loginWindow, "id:btnLogin", TimeSpan.FromSeconds(3)), TimeSpan.FromSeconds(5));
            await session.RunAsync(() => interaction.Invoke(btnLogin), TimeSpan.FromSeconds(5));

            // 5. Detect and Respond to Modal Dialog (MessageBox)
            var modalInfo = await session.RunAsync(() => session.DetectBlockingModal(loginWindow), TimeSpan.FromSeconds(5));
            Assert.True(modalInfo.HasModal, "Modal dialog should be active after failed login.");

            var respondResult = await session.RunAsync(() => interaction.DialogRespond("OK"), TimeSpan.FromSeconds(5));
            Assert.True(respondResult.Success);

            // 6. Test Correct Login -> Open MainForm
            await session.RunAsync(() =>
            {
                var txtUser = locator.Resolve(loginWindow, "id:txtUsername", TimeSpan.FromSeconds(5));
                var txtPass = locator.Resolve(loginWindow, "id:txtPassword", TimeSpan.FromSeconds(5));
                var btnLogin = locator.Resolve(loginWindow, "id:btnLogin", TimeSpan.FromSeconds(5));

                interaction.SetValue(txtUser, "admin");
                interaction.SetValue(txtPass, "123456");
                interaction.Invoke(btnLogin);
                return true;
            }, TimeSpan.FromSeconds(10));

            // 7. Resolve MainForm
            var mainWindow = await session.RunAsync(() =>
            {
                Thread.Sleep(500);
                return session.ResolveWindow("name~:Quản lý Đơn hàng");
            }, TimeSpan.FromSeconds(10));
            Assert.NotNull(mainWindow);

            // 8. Read DataGridView
            var gridResult = await session.RunAsync(() =>
            {
                var dgOrders = locator.Resolve(mainWindow, "id:dgOrders", TimeSpan.FromSeconds(5));
                return interaction.GridRead(dgOrders, startRow: 0, maxRows: 5, maxCols: 4);
            }, TimeSpan.FromSeconds(10));
            Assert.True(gridResult.Success);
            Assert.Contains("DH0001", gridResult.Message);

            // 9. Modify DataGridView Cell
            var setCellResult = await session.RunAsync(() =>
            {
                var dgOrders = locator.Resolve(mainWindow, "id:dgOrders", TimeSpan.FromSeconds(5));
                return interaction.GridSetCell(dgOrders, row: 0, col: 1, value: "VIP Customer");
            }, TimeSpan.FromSeconds(10));
            Assert.True(setCellResult.Success);

            // 9b. Duyệt cây UI của MainForm — form này có MenuStrip/ToolStripMenuItem,
            //     các control không cung cấp property AutomationId [#30011].
            //     Trước khi sửa, TreeSerializer/ElementDto ném PropertyNotSupportedException tại đây.
            var (mainTree, _) = await session.RunAsync(
                () => serializer.Serialize(mainWindow, maxDepth: 6),
                TimeSpan.FromSeconds(20));
            Assert.False(string.IsNullOrWhiteSpace(mainTree), "Cây UI của MainForm không được rỗng.");
            Assert.Contains("dgOrders", mainTree);

            var (shallowTree, _) = await session.RunAsync(
                () => serializer.Serialize(mainWindow, maxDepth: 2),
                TimeSpan.FromSeconds(20));
            Assert.False(string.IsNullOrWhiteSpace(shallowTree));

            // 9c. ElementDto phải dựng được cho MỌI descendant, kể cả menu item thiếu AutomationId.
            var dtoCount = await session.RunAsync(() =>
            {
                var all = mainWindow.FindAllDescendants();
                var count = 0;
                foreach (var el in all)
                {
                    var dto = ElementDto.FromAutomationElement(el);
                    Assert.NotNull(dto.Type);
                    count++;
                }
                return count;
            }, TimeSpan.FromSeconds(30));
            Assert.True(dtoCount > 0, "Phải đọc được ít nhất một element trên MainForm.");

            // 9d. Tìm chính menu item gây lỗi ban đầu.
            var menuItems = await session.RunAsync(
                () => locator.ResolveAll(mainWindow, "type:MenuItem", limit: 20).Count,
                TimeSpan.FromSeconds(20));
            Assert.True(menuItems > 1, $"MainForm có nhiều menu item, ResolveAll chỉ trả về {menuItems}.");

            // 10. Capture Screenshot
            var screenshot = await session.RunAsync(() => screenshotService.Capture(mainWindow, maxWidth: 1000), TimeSpan.FromSeconds(10));
            Assert.NotNull(screenshot.Bytes);
            Assert.True(screenshot.Bytes.Length > 0);
            Assert.True(screenshot.Bytes.Length < 4 * 1024 * 1024); // < 4MB
        }
        finally
        {
            // Clean up: close application
            session.Dispose();
        }
    }
}
