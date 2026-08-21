# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**WinForms Verifier** — an MCP (Model Context Protocol) server on .NET 8 (`net8.0-windows`, x64) that gives AI agents three capabilities over Windows Forms apps: live UI automation (FlaUI/UIA3), screenshot capture with downscaling, and Roslyn static analysis of Designer code. 26 tools, all prefixed `wf_`.

Key packages: `ModelContextProtocol 2.2.0`, `FlaUI.UIA3 5.0.0`, `Microsoft.CodeAnalysis.CSharp.Workspaces 4.8.0`, `Microsoft.Extensions.Hosting 8.0.1`.

## Commands

```bash
dotnet build                       # whole solution
dotnet test                        # all 18 tests (unit + integration)
dotnet test tests/WinFormsVerifier.UnitTests
dotnet test --filter "FullyQualifiedName~RoslynRuleTests"
dotnet test --filter "DisplayName~FullInteractiveWorkflow"

# publish the binary the MCP clients actually launch
dotnet publish src/WinFormsVerifier.McpServer -c Release -r win-x64 --self-contained false -o dist
```

`LiveUiWorkflowTests` launches `samples/SampleApp/bin/Debug/net8.0-windows/SampleApp.exe` and drives a real window — build SampleApp (Debug) first, and expect the test to open UI on the desktop.

**Never run the server with `dotnet run`.** MSBuild writes to stdout and corrupts the JSON-RPC stream. Always launch the published `dist/WinFormsVerifier.McpServer.exe`. After changing server code, re-publish to `dist/` or MCP clients keep running the stale binary (`.mcp.json` in this repo registers that exact path, so this repo's own `wf_*` tools go stale too).

## Architecture

Request flow: **MCP client → stdio JSON-RPC → Tool (thin) → `session.RunAsync` → STA thread → FlaUI/UIA3**.

- `Program.cs` — `Host.CreateApplicationBuilder` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`. Registers all domain services as singletons and wires `ApplicationStopping` + `ProcessExit` to `UiSession.Dispose()`.
- `Tools/*.cs` — `public static` classes marked `[McpServerToolType]`, methods marked `[McpServerTool(Name = "wf_...")]`. Domain services appear as leading method parameters and are DI-resolved (they do **not** leak into the tool's inputSchema). Every method ends with `CancellationToken ct = default`.
- `Services/UiThread.cs` — the single named `UIA-STA` thread with a `BlockingCollection<Action>` pump. `RunAsync<T>(work, timeout, ct)` marshals work onto it. Tracks `_consecutiveTimeouts`: two in a row escalates the error message to "session is poisoned, close and re-attach".
- `Services/UiSession.cs` — owns `Application` + `UIA3Automation`. `ResolveWindow(selector)` prefers an active modal over the main window. `DetectBlockingModal` uses raw Win32 `EnumWindows` looking for class `#32770` (faster and more reliable than UIA for MessageBox).
- `Services/ElementLocator.cs` — parses selectors, narrows scope segment by segment with `Retry.WhileNull`; on failure computes Levenshtein similarity over visible `Name`/`AutomationId` and returns the top 10 as `candidates` in the error envelope.
- `Services/Roslyn/` — `DesignerModel` parses `InitializeComponent()` into a control tree; `FormRules` runs the 14 `WF001`–`WF060` checks; `FormAnalyzer` clusters the partial-class files (`Form1.cs` + `Form1.Designer.cs` + `Form1.*.cs`) into a tolerant `CSharpCompilation` (missing references are acceptable).

### Selector syntax

Hierarchical, `>`-separated: `id:grid > grid:2,3`. Segment prefixes: `id:`, `name:`, `name~:` (contains), `type:`, `class:`, `help:`, `idx:`, `grid:row,col`. No prefix means `name:`, or `id:` if it starts with `#`. Window selectors (`UiSession.FindWindowBySelector`) use a smaller set: `id:`, `name:`, `name~:`, `class:`, `title:`, `title~:`.

## Invariants (from AGENTS.md and .agents/rules/)

1. **stdout is JSON-RPC only.** No `Console.WriteLine`. All logging goes to stderr via `AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)`; `WFVERIFY_LOG_LEVEL` sets the minimum level.
2. **All UIA3/FlaUI calls run on the STA thread** via `session.RunAsync(...)` with a hard timeout. COM objects are not thread-safe and MCP requests arrive concurrently.
3. **Tools stay thin.** Validate → `McpResults.GuardAsync` → delegate to a domain service. Business logic belongs in `Services/`.
4. **Every tool returns the standard envelope.** `McpResults.Ok(data, warnings)` / `McpResults.Fail(code, message, hint, candidates, details)`; failures set `IsError = true`. Codes live in `Models/ErrorCode.cs`. Throw `ToolException` from services — `GuardAsync` converts it. Selector failures must carry candidates; blocked-by-modal must return `BLOCKED_BY_MODAL` with the dialog title/text/buttons rather than timing out.
5. **PathGuard every path input.** Whitelist from `WFVERIFY_ALLOWED_ROOTS` (`;`-separated), else working dir + upward search for `.sln`/`.git`/`plan.md`. Launched processes use `ProcessStartInfo.ArgumentList`, never concatenated command lines, with `UseShellExecute = false`.
6. **No orphaned processes.** Only kill processes where `LaunchedByUs == true`; dispose `UIA3Automation` to release COM.
7. **Interaction fallback chains** (see `.agents/rules/ui-automation-rules.md`): Invoke → `InvokePattern` → `SelectionItem` → `LegacyIAccessible.DoDefaultAction` → physical click; SetValue → `ValuePattern` → focus + SendKeys; Toggle → `TogglePattern` → `SelectionItem` → Invoke.
8. **Images are raw bytes.** `ImageContentBlock.Data` takes `ReadOnlyMemory<byte>`; the SDK base64-encodes. Downscale before returning (cap ~4MB).

## Conventions

- User-facing strings (tool `[Description]`s, error messages, hints, log messages) are written in **Vietnamese**; identifiers and code comments explaining mechanics are English. Match the surrounding file.
- `[Description]` on every tool *and* every parameter — this is the only spec the calling agent sees. State defaults, formats, and prerequisite tools.
- C# 12, file-scoped namespaces, `Nullable` and `ImplicitUsings` enabled solution-wide via `Directory.Build.props`; `EnforceCodeStyleInBuild` is on (`.editorconfig`).
- `plan.md` is the verified implementation spec (API shapes confirmed against real probes); `docs/WinForms_Verifier_MCP_Implementation_Guide.md` is the superseded v1 guide — prefer `plan.md`.

## Quy tắc báo cáo & xưng hô

- **Chứng minh trước khi báo xong.** Trước khi nói một việc đã hoàn thành, phải đưa ra kết quả cụ thể chứng minh điều đó: output của `dotnet test`/`dotnet build`, nội dung file sau khi sửa, JSON envelope mà tool trả về, hoặc ảnh chụp màn hình. Không có bằng chứng thì không được nói "xong".
- **Chỉ báo cáo những việc có bằng chứng.** Không liệt kê thành quả suy đoán, không mô tả hành vi chỉ dựa trên việc đọc code mà chưa chạy.
- **Chưa verify được thì nói thẳng.** Nêu rõ "chưa verify" kèm lý do (không chạy được trên môi trường này, cần UI thật, cần quyền, v.v.) thay vì đoán hoặc nói mơ hồ.
- **Xưng hô:** khi trả lời, tự xưng là **tôi** và gọi người dùng là **Anhzai**.
