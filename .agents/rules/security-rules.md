# Security & PathGuard Rules

1. **PathGuard Whitelisting:**
   - All file and directory paths passed to tools (`wf_launch_app`, `wf_analyze_form`, `wf_analyze_project`) must be validated against `PathGuard`.
   - The whitelist is loaded from `WFVERIFY_ALLOWED_ROOTS` environment variable (semicolon-separated). If unset, defaults to the server working directory.
   - Paths outside the whitelist must be rejected immediately with `ErrorCode.PathDenied`.

2. **Process Execution Safety:**
   - Always use `ProcessStartInfo.ArgumentList` to pass command line arguments to launched processes. Never construct command line strings by concatenation.
   - `UseShellExecute` must be `false`.

3. **Lifecycle & Session Isolation:**
   - Only processes launched by the server (`LaunchedByUs == true`) may be terminated by `wf_close_app` or host shutdown hooks. `wf_close_app` **enforces this**: attaching to a process a human started (typically already logged in or pointed at a specific SQL environment) and then closing it destroys work that cannot be rebuilt, so the tool refuses and points at `wf_detach_app`.
   - `wf_detach_app` releases the session without touching the process. It is the correct way to end work on an attached application.
   - The server must not kill arbitrary processes by name.
