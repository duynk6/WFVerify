using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace WinFormsVerifier.IntegrationTests;

/// <summary>
/// Nói chuyện JSON-RPC thật với exe đã publish qua stdio — đúng cách MCP client làm.
/// Đây là lưới an toàn rẻ nhất cho quy tắc "stdout chỉ dành cho JSON-RPC": chỉ cần một
/// Console.WriteLine lọt vào đâu đó là handshake vỡ và test này đỏ ngay.
/// Không cần desktop session, không mở UI.
/// </summary>
public class ProtocolSmokeTests
{
    private static string? FindServerExe()
    {
        var overridePath = Environment.GetEnvironmentVariable("WFVERIFY_SERVER_EXE");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return File.Exists(overridePath) ? overridePath : null;
        }

        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "dist", "WinFormsVerifier.McpServer.exe")),
            @"E:\AgentTest\WFVerify\dist\WinFormsVerifier.McpServer.exe",
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private sealed class ServerPipe : IDisposable
    {
        private readonly Process _proc;
        private int _id;

        public ServerPipe(string exePath)
        {
            _proc = Process.Start(new ProcessStartInfo(exePath)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8,
            })!;
        }

        public void Notify(string method)
            => Write(new { jsonrpc = "2.0", method });

        public JsonElement Request(string method, object? @params = null)
        {
            var id = ++_id;
            Write(new { jsonrpc = "2.0", id, method, @params = @params ?? new { } });

            var line = _proc.StandardOutput.ReadLine();
            Assert.False(string.IsNullOrWhiteSpace(line),
                $"Server không trả gì cho '{method}'. stdout có thể đã bị log làm hỏng.");

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(line!);
            }
            catch (JsonException ex)
            {
                Assert.Fail($"stdout KHÔNG phải JSON-RPC hợp lệ — nhiều khả năng có log lọt vào stdout.\nDòng nhận được: {line}\nLỗi: {ex.Message}");
                throw;
            }

            return doc.RootElement.Clone();
        }

        private void Write(object payload)
        {
            _proc.StandardInput.Write(JsonSerializer.Serialize(payload) + "\n");
            _proc.StandardInput.Flush();
        }

        public void Dispose()
        {
            try
            {
                _proc.StandardInput.Close();
                if (!_proc.WaitForExit(5000)) _proc.Kill(true);
            }
            catch { /* ignore */ }
            _proc.Dispose();
        }
    }

    [Fact]
    public void Handshake_ThenToolsList_ThenPing_OverRealStdio()
    {
        var exe = FindServerExe();
        Assert.True(exe != null,
            "Chưa có dist/WinFormsVerifier.McpServer.exe. Chạy: dotnet publish src/WinFormsVerifier.McpServer -c Release -r win-x64 --self-contained false -o dist");

        using var server = new ServerPipe(exe!);

        // 1. initialize
        var init = server.Request("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "protocol-smoke", version = "1.0" }
        });
        Assert.Equal("WinFormsVerifier.McpServer",
            init.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

        server.Notify("notifications/initialized");

        // 2. tools/list — mọi tool phải có tên wf_* và có description cho agent đọc
        var tools = server.Request("tools/list").GetProperty("result").GetProperty("tools");
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()!).ToList();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.StartsWith("wf_", n, StringComparison.Ordinal));
        foreach (var expected in new[] { "wf_ping", "wf_launch_app", "wf_attach_app", "wf_detach_app", "wf_get_ui_tree", "wf_screenshot", "wf_analyze_form" })
        {
            Assert.Contains(expected, names);
        }

        // Mô tả tool là spec duy nhất agent nhìn thấy — không được để trống.
        foreach (var tool in tools.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString();
            Assert.True(tool.TryGetProperty("description", out var d) && !string.IsNullOrWhiteSpace(d.GetString()),
                $"Tool '{name}' thiếu [Description].");
        }

        // 3. tools/call wf_ping — không cần app nào đang chạy
        var ping = server.Request("tools/call", new { name = "wf_ping", arguments = new { } });
        var text = ping.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString();

        using var envelope = JsonDocument.Parse(text!);
        Assert.True(envelope.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("healthy", envelope.RootElement.GetProperty("data").GetProperty("status").GetString());
    }
}
