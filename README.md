# WinForms Verifier MCP Server

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0%20Windows-blue.svg)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/Tests-18%20Passed-brightgreen.svg)]()
[![MCP Version](https://img.shields.io/badge/MCP-2.2.0-indigo.svg)](https://modelcontextprotocol.io/)

**WinForms Verifier** là một Model Context Protocol (MCP) server hiệu năng cao chạy trên nền tảng .NET 8 (Windows x64), cung cấp khả năng tự động hóa kiểm thử giao diện người dùng (UI Automation với FlaUI/UIA3), kiểm chứng thị giác (Visual Verification & Vision Downscaling) và phân tích tĩnh chuyên sâu mã nguồn Windows Forms bằng Roslyn AST cho các AI Coding Agents.

> 📖 **Xem tài liệu hướng dẫn trực quan đầy đủ tại:** [`docs/index.html`](file:///e:/AgentTest/WFVerify/docs/index.html) *(Mở trực tiếp trên trình duyệt)*

---

## 🌟 Tính năng Nổi bật

1. **Cách ly STDOUT / STDERR Tuyệt đối:**
   - STDOUT được bảo vệ nghiêm ngặt chỉ dành riêng cho stream JSON-RPC của MCP. Toàn bộ log của server và runtime được điều hướng 100% sang STDERR.
2. **Luồng STA Chuyên trách & Chống Deadlock (Single STA Message Pump):**
   - Đảm bảo an toàn luồng COM cho FlaUI và UIA3.
   - Tích hợp cơ chế **Poison Detection** (cảnh báo session hỏng sau 2 lần timeout liên tiếp).
   - Tự động phát hiện và chặn lỗi `BLOCKED_BY_MODAL` khi ứng dụng hiển thị hộp thoại `MessageBox`.
3. **Bộ 27 Công cụ MCP Toàn diện:**
   - **Diagnostics:** `wf_ping` (kiểm tra runtime, session, DPI scale).
   - **App Lifecycle:** `wf_launch_app` (hỗ trợ `arguments` + biến môi trường), `wf_attach_app` (chờ cửa sổ, tìm theo tiêu đề của mọi cửa sổ), `wf_list_windows`, `wf_detach_app`, `wf_close_app`.
   - **UI Inspection:** `wf_get_ui_tree` (cây UI compact text tiết kiệm ~55% token), `wf_find_elements`, `wf_get_element`.
   - **UI Interaction:** `wf_invoke`, `wf_set_value`, `wf_toggle`, `wf_select`, `wf_expand`, `wf_send_keys`, `wf_focus`, `wf_scroll_into_view`, `wf_grid_read`, `wf_grid_set_cell`, `wf_menu_click`, `wf_dialog_respond`.
   - **Synchronization:** `wf_wait_for`, `wf_wait_idle`.
   - **Visual Verification:** `wf_screenshot` (chụp cửa sổ/control, downscale giữ tỉ lệ, nén dưới 4MB).
   - **Static Analysis:** `wf_analyze_form`, `wf_analyze_project`, `wf_list_rules`.
4. **Bộ 14 Rule Phân tích Tĩnh Roslyn (`WF001`–`WF060`):**
   - Phân tích cú pháp AST của cụm partial class (`Form.cs` + `Form.Designer.cs`).
   - Bắt chính xác các lỗi: Event handler mồ côi/gãy (`WF001`, `WF002`), đè tọa độ (`WF010`), vượt ClientSize (`WF011`), trùng/sai TabIndex (`WF020`, `WF021`, `WF022`), xung đột Dock & Anchor (`WF030`), thiếu AccessibleName (`WF040`), hardcoded font (`WF050`), thiếu AutoScaleMode (`WF051`), control mồ côi (`WF060`).
5. **Bảo mật & Quản lý Tiến trình:**
   - `PathGuard` kiểm tra whitelist đường dẫn file/project.
   - Tự động dọn dẹp các tiến trình con khi máy chủ MCP tắt hoặc session kết thúc.

---

## 🚀 Cài đặt & Tích hợp vào các AI Client / IDE

File thực thi MCP Server đã được biên dịch sẵn tại:
```
E:\AgentTest\WFVerify\dist\WinFormsVerifier.McpServer.exe
```

### 1. Antigravity IDE
Cấu hình trong file [`.mcp.json`](file:///e:/AgentTest/WFVerify/.mcp.json) ở thư mục gốc workspace hoặc `.agents/mcp_config.json`:
```json
{
  "mcpServers": {
    "winforms-verifier": {
      "command": "E:\\AgentTest\\WFVerify\\dist\\WinFormsVerifier.McpServer.exe",
      "args": [],
      "env": {
        "WFVERIFY_ALLOWED_ROOTS": "E:\\AgentTest;C:\\Projects",
        "WFVERIFY_LOG_LEVEL": "Information"
      }
    }
  }
}
```

### 2. Cursor IDE
- **Cách 1 (File dự án):** Tạo file `.cursor/mcp.json` trong workspace:
```json
{
  "mcpServers": {
    "winforms-verifier": {
      "command": "E:\\AgentTest\\WFVerify\\dist\\WinFormsVerifier.McpServer.exe",
      "args": [],
      "env": {
        "WFVERIFY_ALLOWED_ROOTS": "E:\\AgentTest;C:\\Projects"
      }
    }
  }
}
```
- **Cách 2 (Giao diện Settings):** Mở `Cursor Settings > Features > MCP > Add New MCP Server`:
  - **Name:** `winforms-verifier`
  - **Type:** `command`
  - **Command:** `E:\AgentTest\WFVerify\dist\WinFormsVerifier.McpServer.exe`

### 3. Codex & Claude Code (CLI)
Chạy lệnh trong terminal để đăng ký server:
```bash
claude mcp add winforms-verifier -- E:\AgentTest\WFVerify\dist\WinFormsVerifier.McpServer.exe
```

### 4. Claude Desktop App
Thêm vào file `%APPDATA%\Claude\claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "winforms-verifier": {
      "command": "E:\\AgentTest\\WFVerify\\dist\\WinFormsVerifier.McpServer.exe",
      "args": [],
      "env": {
        "WFVERIFY_ALLOWED_ROOTS": "E:\\AgentTest;C:\\Projects",
        "WFVERIFY_LOG_LEVEL": "Information"
      }
    }
  }
}
```

### 5. VS Code (Extension Roo Code / Cline / Continue)
Thêm vào `cline_mcp_settings.json` hoặc `roo_code_mcp_settings.json`:
```json
{
  "mcpServers": {
    "winforms-verifier": {
      "command": "E:\\AgentTest\\WFVerify\\dist\\WinFormsVerifier.McpServer.exe",
      "args": [],
      "env": {
        "WFVERIFY_ALLOWED_ROOTS": "E:\\AgentTest;C:\\Projects"
      },
      "disabled": false,
      "autoApprove": []
    }
  }
}
```

---

## 🛠️ Build & Test

### Chạy toàn bộ Test Suite (18 Tests)
```bash
dotnet test
```

### Xuất bản Binary (Publish Release)
```bash
dotnet publish src/WinFormsVerifier.McpServer -c Release -r win-x64 --self-contained false -o dist
```

---

## 📁 Cấu trúc Thư mục

```
WFVerify/
├── WinFormsVerifier.sln
├── dist/WinFormsVerifier.McpServer.exe       # Binary MCP Server xuất xưởng
├── docs/index.html                          # Tài liệu HTML hướng dẫn sử dụng tương tác
├── src/WinFormsVerifier.McpServer/          # Source code MCP Server
│   ├── app.manifest                         # DPI Awareness & LongPathAware
│   ├── Program.cs                           # Host bootstrap, Stdio transport, DI
│   ├── Infrastructure/                      # PathGuard, McpResults, ToolException
│   ├── Models/                              # Selector, ElementDto, ErrorCode, Diagnostic
│   ├── Services/                            # UiThread, UiSession, ElementLocator,
│   │                                        # InteractionService, ScreenshotService,
│   │                                        # TreeSerializer, FormAnalyzer, FormRules
│   └── Tools/                               # 26 MCP Tools theo từng nhóm chức năng
├── samples/SampleApp/                       # WinForms Test Fixture
└── tests/
    ├── WinFormsVerifier.UnitTests/          # 14 Unit tests
    └── WinFormsVerifier.IntegrationTests/   # 4 Integration & Live UI Workflow tests
```

---

## 📄 Bản quyền & Quy chuẩn
Tuân thủ các quy định và kiến trúc tại [`AGENTS.md`](file:///e:/AgentTest/WFVerify/AGENTS.md) và [`.agents/rules/`](file:///e:/AgentTest/WFVerify/.agents/rules/).
Chi tiết lịch sử phát triển xem tại [`CHANGELOG.md`](file:///e:/AgentTest/WFVerify/CHANGELOG.md).
