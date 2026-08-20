# Changelog — WinForms Verifier MCP Server

Tất cả các thay đổi, bổ sung và quá trình triển khai dự án **WinForms Verifier MCP Server** được ghi lại tại tài liệu này.

---

## [1.0.0] - 2026-08-20

### 🚀 Khởi tạo & Triển khai toàn diện (Initial Implementation)

Đã hoàn thành triển khai toàn bộ hệ sinh thái WinForms Verifier MCP Server (.NET 8 Windows x64) theo đặc tả kiến trúc `plan.md`.

#### 1. Hệ thống Quy chuẩn, Cấu hình & Quản trị (.agents & rules)
- **Cấu hình Solution & Code Style:**
  - [`.gitignore`](file:///e:/AgentTest/WFVerify/.gitignore): Thiết lập loại trừ build artifacts, cache IDE, log, test results.
  - [`.editorconfig`](file:///e:/AgentTest/WFVerify/.editorconfig): Quy chuẩn code style C# 12 / .NET 8 (file-scoped namespaces, naming conventions, formatting).
  - [`Directory.Build.props`](file:///e:/AgentTest/WFVerify/Directory.Build.props): Kích hoạt `Nullable=enable`, `ImplicitUsings=enable`, `LangVersion=12.0` cho mọi project.
  - [`.mcp.json`](file:///e:/AgentTest/WFVerify/.mcp.json): Cấu hình client MCP cho Claude Code / Cursor kết nối trực tiếp với binary `dist/WinFormsVerifier.McpServer.exe`.
- **Hệ thống Rule & Guideline:**
  - [`AGENTS.md`](file:///e:/AgentTest/WFVerify/AGENTS.md): Bản quy tắc kiến trúc cốt lõi (bảo vệ luồng `stdout`, bắt buộc STA thread cho UIA3, poison detection, dọn dẹp tiến trình).
  - [`.agents/rules/mcp-stdio-rules.md`](file:///e:/AgentTest/WFVerify/.agents/rules/mcp-stdio-rules.md): Cách ly tuyệt đối `stdout` cho JSON-RPC, toàn bộ log ra `stderr`.
  - [`.agents/rules/ui-automation-rules.md`](file:///e:/AgentTest/WFVerify/.agents/rules/ui-automation-rules.md): Quy định về UI Automation, selector phân cấp, chuỗi fallback tương tác và chặn modal dialog.
  - [`.agents/rules/static-analysis-rules.md`](file:///e:/AgentTest/WFVerify/.agents/rules/static-analysis-rules.md): Quy định phân tích tĩnh Roslyn và bộ 14 rule `WF001`–`WF060`.
  - [`.agents/rules/security-rules.md`](file:///e:/AgentTest/WFVerify/.agents/rules/security-rules.md): Bảo mật whitelist đường dẫn `PathGuard` và ngăn ngừa command injection.

---

#### 2. Kiến trúc Lõi & Hạ tầng MCP Server
- **DPI & Windows Compatibility:**
  - [`app.manifest`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/app.manifest): Kích hoạt `PerMonitorV2` DPI awareness và `longPathAware`.
- **Hạ tầng Protocol & Bảo mật:**
  - [`PathGuard.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Infrastructure/PathGuard.cs): Kiểm tra whitelist đường dẫn an toàn từ biến môi trường `WFVERIFY_ALLOWED_ROOTS` hoặc thư mục workspace.
  - [`ToolException.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Infrastructure/ToolException.cs) & [`ErrorCode.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Models/ErrorCode.cs): Định nghĩa mã lỗi chuẩn (`NO_SESSION`, `APP_EXITED`, `ELEMENT_NOT_FOUND`, `AMBIGUOUS`, `PATTERN_UNSUPPORTED`, `TIMEOUT`, `BLOCKED_BY_MODAL`, `PATH_DENIED`, `INTERNAL`).
  - [`McpResults.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Infrastructure/McpResults.cs): Helper `GuardAsync`, đóng gói envelope `{ ok: true, data: ..., warnings: [...] }` hoặc `{ ok: false, error: ... }` kèm `IsError = true`.
  - [`Program.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Program.cs): Bootstrap Host với `WithStdioServerTransport()`, cấu hình `AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)` và đăng ký dọn dẹp tiến trình mồ côi khi tắt server.

---

#### 3. Domain Services cho UI Automation & Phân tích tĩnh
- **UI Threading & Session:**
  - [`UiThread.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/UiThread.cs): Quản lý luồng STA duy nhất với message pump, timeout cứng và poison detection (2 lần timeout liên tiếp cảnh báo session hỏng).
  - [`UiSession.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/UiSession.cs): Quản lý Application và UIA3Automation, tự động tìm modal dialog active trước main window, giải phóng unmanaged COM và kill process con do server khởi chạy khi kết thúc.
- **Thanh tra & Tương tác:**
  - [`Selector.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Models/Selector.cs): Hỗ trợ cú pháp selector phân cấp `prefix:value > prefix:value` (`id:`, `name:`, `name~:`, `type:`, `class:`, `help:`, `idx:`, `grid:`).
  - [`ElementLocator.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/ElementLocator.cs): Tìm kiếm qua `Retry.WhileNull`, thu hẹp scope từng bước, và tự động tính khoảng cách Levenshtein gợi ý 10 ứng viên gần nhất khi sai selector.
  - [`TreeSerializer.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/TreeSerializer.cs): Định dạng cây UI text compact thụt lề (tiết kiệm ~55% token so với JSON phẳng), có cảnh báo khi chạm `maxDepth`/`maxNodes`.
  - [`InteractionService.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/InteractionService.cs): Chuỗi fallback thông minh (`Invoke` -> `SelectionItem` -> `LegacyIAccessible` -> `Click`), nhập liệu có kiểm tra lại (`verify`), đọc/ghi ô `DataGridView`, click menu phân cấp và tự động phát hiện `BLOCKED_BY_MODAL`.
  - [`ScreenshotService.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/ScreenshotService.cs): Chụp ảnh màn hình cửa sổ hoặc control, tự động scale giữ tỷ lệ, nén PNG/JPEG dưới 4MB và trả về `ImageContentBlock` dạng raw bytes.
- **Phân tích tĩnh Roslyn:**
  - [`DesignerModel.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/Roslyn/DesignerModel.cs): Phân tích cú pháp AST của `InitializeComponent()` thành cây `DesignerControlNode` (tọa độ, kích thước, font, tab index, dock, anchor, event wirings, container hierarchy).
  - [`FormRules.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/Roslyn/FormRules.cs): Bộ 14 rule tĩnh (`WF001`–`WF060`):
    - `WF001` (Error): Handler được gán nhưng method không tồn tại trong code-behind.
    - `WF002` (Warning): Method dạng handler nhưng không được gán vào sự kiện nào.
    - `WF010` (Warning): Hai control cùng container bị đè tọa độ lên nhau.
    - `WF011` (Warning): Control nằm ngoài ClientSize của container cha.
    - `WF012` (Info): Control có tọa độ Location âm.
    - `WF020` (Warning): Trùng TabIndex giữa các control tương tác cùng cấp.
    - `WF021` (Info): TabIndex không liên tục hoặc control tương tác thiếu TabIndex.
    - `WF022` (Info): Thứ tự TabIndex không khớp thứ tự đọc trực quan.
    - `WF030` (Error): `Dock = Fill` kết hợp với `Anchor` khác mặc định.
    - `WF031` (Warning): Control trong container resize được nhưng giữ Anchor mặc định `Top, Left`.
    - `WF040` (Warning): Control tương tác thiếu `AccessibleName` và `Text` rỗng/mặc định.
    - `WF041` (Info): Control vẫn giữ tên mặc định (`button1`, `textBox2`).
    - `WF050` (Warning): Font hardcode trên control khác với font Form.
    - `WF051` (Info): Form chưa đặt `AutoScaleMode` hoặc đang là `None`.
    - `WF060` (Info): Control được khởi tạo nhưng không thêm vào `Controls.Add`.
  - [`FormAnalyzer.cs`](file:///e:/AgentTest/WFVerify/src/WinFormsVerifier.McpServer/Services/Roslyn/FormAnalyzer.cs): Phân tích theo cụm partial class (`Form.cs` + `Form.Designer.cs`) và quét toàn bộ project `.csproj`.

---

#### 4. Danh mục 26 MCP Tools Đầy đủ
| Nhóm | Tool | Mô tả |
| :--- | :--- | :--- |
| **Diagnostics** | `wf_ping` | Health check: trạng thái server, session, runtime, DPI scale màn hình chính. |
| **Lifecycle** | `wf_launch_app` | Khởi chạy ứng dụng WinForms (.exe) có whitelist và chờ cửa sổ chính. |
| | `wf_attach_app` | Attach vào ứng dụng đang chạy qua PID, tên tiến trình hoặc tiêu đề cửa sổ. |
| | `wf_list_windows` | Liệt kê toàn bộ top-level windows và modal dialogs. |
| | `wf_close_app` | Đóng/kill ứng dụng và dọn dẹp tài nguyên session. |
| **Inspection** | `wf_get_ui_tree` | Lấy cây UI dạng text compact thụt lề. |
| | `wf_find_elements` | Tìm kiếm danh sách elements thỏa mãn selector. |
| | `wf_get_element` | Chi tiết 1 element + danh sách pattern khả dụng. |
| **Interaction** | `wf_invoke` | Click/kích hoạt control qua chuỗi fallback. |
| | `wf_set_value` | Nhập text vào TextBox/Edit control (có chế độ replace/append và verify). |
| | `wf_toggle` | Bật/tắt CheckBox hoặc RadioButton. |
| | `wf_select` | Chọn mục trong ComboBox, ListBox, TabControl, TreeView theo tên/index. |
| | `wf_expand` | Mở rộng/thu gọn TreeView node hoặc ComboBox dropdown. |
| | `wf_send_keys` | Gửi phím bấm thô (`^s`, `{ENTER}`, `%{F4}`). |
| | `wf_focus` | Đặt tiêu điểm (focus) vào control. |
| | `wf_scroll_into_view` | Cuộn danh sách tới control mục tiêu. |
| | `wf_grid_read` | Đọc dữ liệu DataGridView thành bảng text có cấu trúc dòng/cột. |
| | `wf_grid_set_cell` | Chỉnh sửa giá trị ô (cell) trong DataGridView. |
| | `wf_menu_click` | Click mục trong MenuStrip theo đường dẫn phân cấp (`File > Đơn hàng`). |
| | `wf_dialog_respond` | Phản hồi và đóng Modal Dialog (`OK`, `Cancel`, `Yes`, `No`). |
| **Synchronization** | `wf_wait_for` | Chờ control đạt trạng thái `exists`, `visible`, `enabled`, `gone`. |
| | `wf_wait_idle` | Chờ ứng dụng xử lý xong tác vụ nền (WaitWhileBusy). |
| **Visual** | `wf_screenshot` | Chụp ảnh cửa sổ/control đưa vào AI Vision, tự động downscale. |
| **Static Analysis** | `wf_analyze_form` | Phân tích tĩnh cụm partial class của Form bằng Roslyn. |
| | `wf_analyze_project` | Quét và phân tích toàn bộ Form trong project `.csproj`. |
| | `wf_list_rules` | Liệt kê toàn bộ rule tĩnh kèm hướng dẫn khắc phục. |

---

#### 5. Ứng dụng mẫu (SampleApp) & Bộ kiểm thử Toàn diện
- **SampleApp Fixture:**
  - `LoginForm`: Đăng nhập đúng (`admin`/`123456`) -> mở `MainForm`, sai -> bật `MessageBox` báo lỗi.
  - `MainForm`: `MenuStrip`, `TabControl` (3 tab: Quản lý đơn hàng với `DataGridView` 50 dòng, Tác vụ chậm với nút delay 3s, Giới thiệu).
  - `BadLayoutForm`: Form cố tình vi phạm các rule `WF002`, `WF010`, `WF020`, `WF030`, `WF040`, `WF041`, `WF050`, `WF051`, `WF060` làm ground truth cho kiểm thử static analysis.
- **Bộ Kiểm thử Tự động (18/18 Tests Passed - 100% Green):**
  - Unit Tests (14 tests): `SelectorTests` (5 tests), `LevenshteinCandidateTests` (4 tests), `RoslynRuleTests` (3 tests), `PathGuardTests` (2 tests).
  - Integration Tests (4 tests):
    - `SmokeTests` (3 tests): Phân tích project, serialize envelope OK/Error.
    - `LiveUiWorkflowTests` (1 test): Kiểm thử tương tác đầu cuối thực tế trên SampleApp (mở app -> nhập sai pass -> bắt `BLOCKED_BY_MODAL` -> bấm `OK` trên dialog qua Win32 non-blocking -> nhập đúng pass -> mở `MainForm` -> đọc `DataGridView` -> sửa ô dữ liệu -> chụp ảnh màn hình < 4MB -> đóng app an toàn).
  - Protocol Smoke Test: Kiểm chứng pipe JSON-RPC qua Stdio trên bản xuất bản `dist/WinFormsVerifier.McpServer.exe`.

---

#### 6. Tài liệu Hướng dẫn & Giao diện HTML
- [`docs/index.html`](file:///e:/AgentTest/WFVerify/docs/index.html): Trang tài liệu HTML tương tác hiện đại, chuẩn UI/UX, hỗ trợ tìm kiếm nhanh 26 tool và 14 rule, hướng dẫn cấu hình chi tiết cho **Antigravity IDE**, **Cursor IDE**, **Codex / Claude Code**, **Claude Desktop** và **VS Code** kèm nút bấm 1-click copy.
- [`README.md`](file:///e:/AgentTest/WFVerify/README.md): Tài liệu tổng quan dự án, kiến trúc và hướng dẫn cài đặt nhanh cho mọi IDE/Client.
