# MCP & Stdio Protocol Rules

1. **Protocol Integrity:**
   - Standard output (`stdout`) is the JSON-RPC wire format. Any character printed to `stdout` that is not a valid JSON-RPC frame will break the MCP connection.
   - All logging providers must be configured with `o.LogToStandardErrorThreshold = LogLevel.Trace` or write directly to `Console.Error`.

2. **Tool Definition Conventions:**
   - Every tool class must be public static and decorated with `[McpServerToolType]`.
   - Every tool method must be public static and decorated with `[McpServerTool(Name = "wf_...")]`.
   - Parameter descriptions must clearly guide the LLM agent: specify default values, formats, and related prerequisite tools.
   - Every tool method must accept a `CancellationToken ct = default`.

3. **Result Envelopes:**
   - Always wrap tool logic in `McpResults.GuardAsync(...)`.
   - Never let unhandled exceptions crash the host or bypass the MCP error envelope.
   - Image content blocks must provide raw bytes (`ReadOnlyMemory<byte>`) rather than base64 strings (the MCP SDK handles encoding).
