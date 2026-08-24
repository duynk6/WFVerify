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
3. **Bộ 28 Công cụ MCP Toàn diện:**
   - **Diagnostics:** `wf_ping` (kiểm tra runtime, session, DPI scale).
   - **App Lifecycle:** `wf_launch_app` (hỗ trợ `arguments`, `argumentsFile`, placeholder `${env:...}` / `${file:...#key}`, biến môi trường), `wf_attach_app` (chờ cửa sổ, tìm theo tiêu đề của mọi cửa sổ), `wf_list_windows`, `wf_detach_app`, `wf_close_app`.
   - **UI Inspection:** `wf_get_ui_tree` (cây UI compact text tiết kiệm ~55% token), `wf_find_elements`, `wf_get_element`.
   - **UI Interaction:** `wf_invoke`, `wf_set_value`, `wf_toggle`, `wf_select`, `wf_expand`, `wf_send_keys`, `wf_focus`, `wf_scroll_into_view`, `wf_grid_read`, `wf_grid_find`, `wf_grid_set_cell`, `wf_menu_click`, `wf_dialog_respond`.
   - **Synchronization:** `wf_wait_for`, `wf_wait_idle`.
   - **Visual Verification:** `wf_screenshot` (chụp cửa sổ/control, downscale giữ tỉ lệ, nén dưới 4MB).
   - **Static Analysis:** `wf_analyze_form`, `wf_analyze_project`, `wf_list_rules`.
4. **Bộ 15 Rule Phân tích Tĩnh Roslyn (`WF001`–`WF060`):**
   - Phân tích cú pháp AST của cụm partial class (`Form.cs` + `Form.Designer.cs`).
   - Bắt chính xác các lỗi: Event handler mồ côi/gãy (`WF001`, `WF002`), đè tọa độ (`WF010`), vượt ClientSize (`WF011`), trùng/sai TabIndex (`WF020`, `WF021`, `WF022`), xung đột Dock & Anchor (`WF030`), thiếu AccessibleName (`WF040`), hardcoded font (`WF050`), thiếu AutoScaleMode (`WF051`), control mồ côi (`WF060`).
5. **Bảo mật & Quản lý Tiến trình:**
   - `PathGuard` kiểm tra whitelist đường dẫn file/project.
   - Tự động dọn dẹp các tiến trình con khi máy chủ MCP tắt hoặc session kết thúc.

---

## 🚀 Cài đặt & Tích hợp vào các AI Client / IDE

### ⚡ Cài đặt nhanh (khuyên dùng)

Thay vì tự publish rồi copy-paste JSON với đường dẫn tuyệt đối, chạy [`install.ps1`](file:///e:/AgentTest/WFVerify/install.ps1) — script tự `dotnet publish`, tự suy ra đường dẫn `.exe` từ vị trí thực tế của repo (không hardcode) và đăng ký thẳng với client:

```powershell
# Claude Code CLI — nhanh nhất, không cần đụng tới file JSON nào
.\install.ps1 -Client claude-code

# Claude Desktop — tự merge vào claude_desktop_config.json (backup file cũ trước khi ghi)
.\install.ps1 -Client claude-desktop

# Cursor / Antigravity — tự merge vào .cursor/mcp.json hoặc .mcp.json của workspace
.\install.ps1 -Client cursor
.\install.ps1 -Client antigravity

# Client khác (VS Code Cline/Roo, ...) — chỉ in JSON sẵn sàng dán, không đụng file nào
.\install.ps1
```

Mặc định chỉ whitelist đúng thư mục repo cho `WFVERIFY_ALLOWED_ROOTS`; truyền `-AllowedRoots "E:\...;C:\..."` nếu cần thêm thư mục. Đã publish rồi thì thêm `-SkipPublish` để chạy lại nhanh. Xem `Get-Help .\install.ps1 -Full` để biết chi tiết từng tham số.

### Cài đặt thủ công

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

## ⚠️ Ràng buộc Môi trường (đọc trước khi dùng)

UI Automation không phải là API chạy ở đâu cũng được. Những ràng buộc dưới đây là **cứng**, không có cách lách:

| Ràng buộc | Chi tiết |
| :--- | :--- |
| **Interactive desktop session** | Cần một phiên desktop **đang mở khoá**. Không chạy được qua SSH, Windows Service, hay CI agent chạy nền. Muốn dùng trong CI phải có RDP session giữ mở hoặc self-hosted runner ở chế độ interactive. |
| **Màn hình khoá** | Khi Windows khoá màn hình, click sẽ thất bại và `wf_screenshot` trả ảnh đen. |
| **Quyền** | Ứng dụng đích chạy elevated (Run as administrator) thì server **cũng phải** elevated, nếu không UIA bị UIPI chặn và mọi thao tác đều thất bại. |
| **DPI** | Server đã bật per-monitor DPI aware v2 qua `app.manifest`. Nếu build lại mà thiếu manifest, `BoundingRectangle` và ảnh chụp sẽ lệch trên máy scale 125/150%. |
| **Bitness** | Server build x64. Attach ứng dụng 32-bit vẫn chạy qua UIA3 (cross-bitness). |
| **Control third-party** | DevExpress / Telerik / Infragistics vẽ custom chỉ hỗ trợ **best-effort** qua `LegacyIAccessible`. Không cam kết. WPF / WinUI / MAUI nằm ngoài phạm vi. |

---

## 🔒 Lưu ý Bảo mật

- **Ảnh chụp màn hình có thể chứa dữ liệu nhạy cảm.** `wf_screenshot` chụp nguyên cửa sổ ứng dụng — bao gồm dữ liệu khách hàng, số liệu tài chính, thông tin đăng nhập đang hiển thị — rồi gửi thẳng cho model. Nếu không chấp nhận được, đặt biến môi trường `WFVERIFY_DISABLE_SCREENSHOT=1` để vô hiệu hoá hẳn tool này.
- **Chế độ chỉ-đọc cho môi trường production.** Đặt `WFVERIFY_READONLY=1` để chặn hẳn `wf_set_value` và `wf_grid_set_cell` (lỗi `READONLY_MODE`), đồng thời chặn `wf_invoke` vào control có nhãn khớp từ khoá ghi dữ liệu — mặc định `Ghi`, `Lưu`, `Xóa`, `Cập nhật`, `Duyệt`, `Save`, `Delete`, `Update`, `Insert`, `Submit`, `Apply`. Tuỳ biến danh sách bằng `WFVERIFY_READONLY_BLOCKLIST` (phân tách bằng `;`). So khớp theo ranh giới từ nên `Nghiên cứu` không bị chặn bởi `Ghi`, nhưng nhãn kiểu `Lưu lượng` thì có (thiên về an toàn).
- **Không đưa mật khẩu vào lời gọi tool.** `wf_launch_app` nhận `argumentsFile` (mảng JSON hoặc file text mỗi dòng một tham số) và placeholder `${env:TEN_BIEN}`, `${file:C:\path\creds.json#sql.password}` trong `arguments`/`environment`; server tự giải trước khi đưa vào `ProcessStartInfo.ArgumentList`.
- **`WFVERIFY_ALLOWED_ROOTS` là hàng rào duy nhất** cho `wf_launch_app`, `wf_analyze_form`, `wf_analyze_project`. Đặt càng hẹp càng tốt; bỏ trống thì mặc định là thư mục làm việc của server. Đường dẫn ngoài whitelist bị chặn với `PATH_DENIED`.
- **Server không giết ứng dụng nó không khởi chạy.** Với app phải đăng nhập / chọn SQL thủ công, dùng `wf_attach_app` rồi kết thúc bằng `wf_detach_app`. `wf_close_app` sẽ **từ chối** nếu tiến trình không do server khởi chạy.
- Không truyền mật khẩu thật qua prompt. Dùng `environment` của `wf_launch_app` hoặc file config cục bộ.

---

## 🧯 Xử lý sự cố (Troubleshooting)

**Mọi tool đều trả `TIMEOUT`, kể cả `wf_list_windows`**
Thường do **một cửa sổ treo của ứng dụng khác** trên cùng desktop (hay gặp nhất: app UWP bị Windows suspend — Notes, To Do, Settings). Bản 1.2.0 đã sửa: server liệt kê cửa sổ theo PID qua Win32 thay vì duyệt toàn bộ desktop. Nếu vẫn gặp trên bản cũ, kiểm tra bằng:
```powershell
Get-Process | Where-Object { $_.MainWindowHandle -ne 0 -and -not $_.Responding } | Select-Object Id,ProcessName
```

**`TIMEOUT` kèm "lần thứ 2 liên tiếp — session có thể đã hỏng"**
Công việc trước đó vẫn đang chiếm luồng STA. Gọi `wf_close_app` (hoặc `wf_detach_app`) rồi launch/attach lại. Timeout chỉ bỏ *chờ*, không huỷ được công việc UIA đang chạy.

**`ELEMENT_NOT_FOUND` dù control hiển thị rõ ràng**
Đọc `candidates` trong envelope lỗi — server đã tính sẵn 10 ứng viên gần nhất. Nếu control không có `Name` lẫn `AutomationId`, chạy `wf_analyze_form` và xem rule `WF040`: nó chỉ đúng chỗ cần đặt `AccessibleName` trong code Designer.

**Thao tác không có tác dụng nhưng tool báo thành công**
Kiểm tra `warnings` trong kết quả. Nếu một `MessageBox` vừa bật lên, server trả cảnh báo kèm nội dung dialog — phải gọi `wf_dialog_respond` trước khi thao tác tiếp.

**Client báo "server disconnected" hoặc `Invalid Base64 string`**
Có thứ gì đó ghi vào stdout ngoài JSON-RPC. Không bao giờ chạy server bằng `dotnet run` (MSBuild in ra stdout) — luôn dùng `dist/WinFormsVerifier.McpServer.exe`. Test `ProtocolSmokeTests` bắt đúng loại lỗi này.

**Sửa code server xong mà hành vi không đổi**
MCP client vẫn đang chạy binary cũ. Publish lại vào `dist/` rồi reload client:
```bash
dotnet publish src/WinFormsVerifier.McpServer -c Release -r win-x64 --self-contained false -o dist
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
