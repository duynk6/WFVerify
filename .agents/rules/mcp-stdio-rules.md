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
   - Image content blocks must be built with `ImageContentBlock.FromBytes(bytes, mimeType)`.
     **Do not assign raw image bytes to `ImageContentBlock.Data`.** In MCP SDK 2.2.0 `Data` holds the *base64-encoded UTF-8 bytes*, not the decoded image; assigning PNG/JPEG bytes directly makes the client reject the response with "Invalid Base64 string". Covered by `ImageContentBlockTests`.
