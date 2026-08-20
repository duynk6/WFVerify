# WinForms Verifier MCP Server — Project Rules & Guidelines

## 1. Project Overview
This repository contains **WinForms Verifier**, a Model Context Protocol (MCP) server running on .NET 8 (Windows x64) that provides runtime inspection, reliable UI automation, visual verification, and Roslyn-based static analysis of Windows Forms applications for AI agents.

## 2. Core Architectural Invariants (DO NOT VIOLATE)

### Rule 1: STDOUT Isolation (Highest Priority)
- **STDOUT is strictly reserved for MCP JSON-RPC protocol messages.**
- NEVER use `Console.WriteLine`, `Console.Write`, or default console logging to standard output.
- All application logging MUST go to **STDERR** (`LogToStandardErrorThreshold = LogLevel.Trace`).
- Never run the server in production/client mode with `dotnet run` (which emits build noise to stdout). Always run the published executable (`dist/WinFormsVerifier.McpServer.exe`).

### Rule 2: Single Dedicated STA Thread for UI Automation
- FlaUI and UIA3 rely on Windows COM interfaces which are NOT thread-safe.
- MCP tool requests may arrive concurrently.
- **ALL UIA3 and FlaUI operations MUST be scheduled onto `UiThread` via `session.RunAsync(...)`.**
- Every automation operation MUST have a strict hard timeout and poison detection (2 consecutive timeouts trigger session recovery instructions).

### Rule 3: Thin Tool Layer & Rich Domain Services
- Tool classes (marked `[McpServerToolType]`) MUST remain thin adapters:
  - Validate parameters.
  - Map exceptions via `McpResults.GuardAsync`.
  - Delegate execution to domain services (`UiSession`, `ElementLocator`, `TreeSerializer`, `InteractionService`, `ScreenshotService`, `FormAnalyzer`).
- Write detailed `[Description]` attributes for tools AND every parameter so AI agents understand exact usage and preconditions.

### Rule 4: Standardized Error Envelopes & Diagnostics
- All tools return JSON envelopes:
  - Success: `{ "ok": true, "data": ..., "warnings": [...] }`
  - Failure: `{ "ok": false, "error": { "code": "...", "message": "...", "hint": "...", "candidates": [...] } }`
- Always set `CallToolResult.IsError = true` on failure so MCP clients can distinguish errors from valid outputs.
- When selector resolution fails, always provide fuzzy matching candidates.
- When blocked by a modal dialog, return `BLOCKED_BY_MODAL` with the dialog text instead of timing out.

### Rule 5: Security & PathGuard Whitelisting
- Every file path, project path, or executable path received as input MUST be validated against `PathGuard`.
- Whitelisted paths are read from `WFVERIFY_ALLOWED_ROOTS` (separated by `;`), defaulting to the server's working directory.
- Arguments passed to launched processes MUST use `ProcessStartInfo.ArgumentList` (never concatenated strings).

### Rule 6: Resource Cleanup & Zero Orphaned Processes
- Any WinForms process launched by the server (`LaunchedByUs == true`) MUST be terminated when the session is closed or when the host shuts down.
- Register cleanup hooks with `IHostApplicationLifetime.ApplicationStopping` and `AppDomain.CurrentDomain.ProcessExit`.
- Dispose `UIA3Automation` cleanly to release unmanaged COM resources.
