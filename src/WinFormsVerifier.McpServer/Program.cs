using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WinFormsVerifier.Services;
using WinFormsVerifier.Services.Roslyn;

var builder = Host.CreateApplicationBuilder(args);

// TỐI QUAN TRỌNG: stdout dành riêng cho JSON-RPC. Mọi log phải ra stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var envLogLevel = Environment.GetEnvironmentVariable("WFVERIFY_LOG_LEVEL");
if (Enum.TryParse<LogLevel>(envLogLevel, true, out var minLevel))
{
    builder.Logging.SetMinimumLevel(minLevel);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

// Đăng ký domain services
builder.Services.AddSingleton<UiThread>();
builder.Services.AddSingleton<UiSession>();
builder.Services.AddSingleton<ElementLocator>();
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<ScreenshotService>();
builder.Services.AddSingleton<TreeSerializer>();
builder.Services.AddSingleton<FormAnalyzer>();

// Đăng ký MCP server với Stdio transport và tự động nạp tools từ assembly
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// Dọn dẹp tài nguyên: không để lại process WinForms mồ côi khi MCP client restart hoặc dừng
var session = host.Services.GetRequiredService<UiSession>();
var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();

lifetime.ApplicationStopping.Register(() => session.Dispose());
AppDomain.CurrentDomain.ProcessExit += (_, _) => session.Dispose();

await host.RunAsync();
