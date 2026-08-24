# Changelog — WinForms Verifier MCP Server

Tất cả các thay đổi, bổ sung và quá trình triển khai dự án **WinForms Verifier MCP Server** được ghi lại tại tài liệu này.

---

## [1.3.0] - 2026-08-22

Đợt sửa theo báo cáo kiểm thử thực tế trên ứng dụng QLSX (DotNetBar + DevExpress + C1FlexGrid, MDI, SQL production).

### 🐞 P0 — lỗi làm sai kết quả kiểm thử

- **`wf_select` báo thành công giả.** `SelectionItem.Pattern.Select()` được gọi rồi trả ngay `"Đã chọn mục 'X'"` mà không đọc lại. Nay mọi tool thao tác đều xác minh hậu điều kiện: `Select` đọc lại `Selection` của container (hoặc giá trị hiển thị), `Toggle` đọc lại `ToggleState`, `Invoke` đọc lại Toggle/SelectionItem nếu control có trạng thái. Không khớp → thử `Click()` vật lý → vẫn không khớp thì trả `warnings` thay vì `ok` trơn.
- **Thông báo lỗi sai khi `index` ngoài phạm vi.** Combo rỗng + `index: 1` rơi xuống nhánh cuối và báo "Cần cung cấp ít nhất tham số 'item' hoặc 'index'". Nhánh `index` nay tách riêng: `"index 1 nằm ngoài phạm vi: danh sách 'cboDonVi' có 0 mục"`.
- **Khớp `item` bằng `Contains` chọn nhầm.** `"May 1"` trúng `"May 10"`, `"Tổ 1"` trúng `"Tổ 11"`. Thêm `Services/ItemMatcher.cs`: khớp chính xác trước, Contains sau; Contains trúng >1 mục → lỗi `AMBIGUOUS` kèm danh sách `[index] tên`.

### 🧩 P1 — control thương mại & form MDI

- **Combo DevExpress/DotNetBar** (lộ ra là `Pane`, không `Selection`, không `ExpandCollapse`, không có `ListItem` con): `Select` tự click bung dropdown rồi tìm `ListItem`/`DataItem`/`TreeItem` trong **cửa sổ popup mới của cùng process** (dropdown của các thư viện này là top-level window riêng, không nằm trong cây của form). Enumerate theo PID bằng Win32, không đi qua desktop UIA. Chọn xong xác minh bằng giá trị đọc lại của chính control; thất bại thì gửi `ESC` để không bỏ lại dropdown chắn màn hình.
- **`wf_grid_read` báo `totalCols = 0`.** C1FlexGrid không hỗ trợ `GridPattern`. Thứ tự suy luận số cột nay là `GridPattern` → header → số ô của dòng đầu tiên; kết quả trả thêm `headers`.
- **`wf_grid_read` chậm.** `GetGridCellElement` dựng lại `AsDataGridView()` cho từng ô (50×20 = 1000 lần). Thay bằng `GridAccessor` cache `DataGridView`, mảng `Rows` và cells theo dòng.
- **Form MDI child trả `WINDOW_NOT_FOUND`.** `windowSelector` chỉ khớp cửa sổ cấp cao nhất. Thêm `NativeWindows.GetMdiChildWindows` (duyệt con trực tiếp của `MDIClient` bằng `GetWindow`, không dùng `EnumChildWindows` vì hàm đó đệ quy cả Button/Label) → `ResolveWindow` và `wf_list_windows` thấy được form con; lỗi `WINDOW_NOT_FOUND` nay liệt kê các form MDI đang mở và gợi ý selector phân cấp.
- **Trùng id giữa các tab.** `id:fg` nay ưu tiên control đang hiển thị (`FirstPreferringVisible`), tức tab đang active; muốn nhắm tab khác thì dùng selector phân cấp `id:tabTheoDoi > id:fg` — đã ghi rõ trong `[Description]` của tham số `windowSelector` và trong `CLAUDE.md`.

### 🔒 P2 — môi trường production

- **`WFVERIFY_READONLY=1`**: chặn `wf_set_value`, `wf_grid_set_cell` (`READONLY_MODE`), và chặn `wf_invoke` vào control có nhãn khớp danh sách ghi dữ liệu (`Ghi`, `Lưu`, `Xóa`, `Cập nhật`, `Duyệt`, `Save`, `Delete`, `Update`, `Insert`, `Submit`, `Apply`; tuỳ biến bằng `WFVERIFY_READONLY_BLOCKLIST`). So khớp theo ranh giới từ nên `Nghiên cứu` không bị chặn bởi `Ghi`.
- **Credential không phải nằm trên command line.** `wf_launch_app` thêm `argumentsFile` (mảng JSON hoặc file text mỗi dòng một tham số) và placeholder `${env:TEN_BIEN}` / `${file:path.json#key.con}` / `${file:path.txt}` dùng được trong cả `arguments` lẫn `environment`. Đường dẫn vẫn đi qua `PathGuard`.
- **➕ `wf_grid_find` (tool mới, tổng số tool: 28)**: tìm dòng theo điều kiện trên một cột (`column` nhận tên header hoặc chỉ số, `op` = `contains`/`equals`/`startswith`), trả `rowIndex` + nội dung dòng. Thay cho việc kéo cả bảng 2000 dòng về rồi tự lọc.

### 🧪 Kiểm thử

- Unit mới: `ItemMatcherTests` (6), `ReadOnlyGuardTests` (12), `ArgumentResolverTests` (8).
- Integration mới `SelectVerifyGridFindTests` (3, chạy trên SampleApp thật): xác minh hậu điều kiện của `Select` + lblFilterResult của app đổi theo, `index` ngoài phạm vi báo đúng, `AMBIGUOUS` khi `"Sản phẩm 1"` trúng 10 mục, khớp chính xác `"Sản phẩm 10"` vẫn chọn được, `GridRead` trả đúng 6 cột × 50 dòng, `GridFind` tìm đúng `DH0007` ở dòng 6, và chế độ chỉ-đọc chặn `set_value`/`invoke` trước khi chạm vào app.
- Tổng: **57 test pass** (44 unit + 13 integration).

### 📖 Tài liệu & DX

- **`install.ps1`** (mới, ở gốc repo): tự `dotnet publish`, tự suy ra đường dẫn `.exe` từ vị trí thực tế của repo (không hardcode) rồi đăng ký thẳng với client — `-Client claude-code` gọi `claude mcp add`, `-Client claude-desktop|cursor|antigravity` tự merge vào file JSON tương ứng kèm backup trước khi ghi đè, mặc định (`-Client print`) chỉ in JSON để dán tay cho client khác. Thay cho việc tự publish rồi copy-paste JSON đường dẫn tuyệt đối theo cách cũ.
- `docs/index.html` cập nhật đồng bộ với 1.3.0: số liệu (28 tools, 15 rules, 57 tests), thêm mục cho `wf_detach_app` và `wf_grid_find` (trước đó thiếu hẳn trong tài liệu dù đã có trong server), thêm phần **Ràng buộc Môi trường & Bảo mật** mà mục lục đã trỏ tới (`#security`) nhưng chưa từng tồn tại, thêm mã lỗi `READONLY_MODE`/`AMBIGUOUS`/`WINDOW_NOT_FOUND` và ghi chú selector cửa sổ MDI/tab-scoping.

---

## [1.2.0] - 2026-08-21

### 🐞 Một cửa sổ treo của ứng dụng KHÁC làm chết toàn bộ server (nghiêm trọng)

- **Triệu chứng:** mọi tool `wf_*` trả `TIMEOUT`, kể cả `wf_list_windows`. Xảy ra đột ngột trên máy vừa chạy tốt vài phút trước.
- **Đo được:** `desktop.FindAllChildren()` mất **60.149 ms** và trả về 12 con; `GetAllTopLevelWindows` sau đó vẫn chưa xong trong 30s còn lại. Trên máy lúc đó có 3 tiến trình UWP bị Windows suspend (`Microsoft.Notes`, `Todo`, `SystemSettings`) với `Responding = False`.
- **Nguyên nhân:** `UiSession.ResolveWindow`, `wf_list_windows` và `wf_launch_app` đều đi qua `Application.GetAllTopLevelWindows` / `GetMainWindow` của FlaUI — các hàm này duyệt con của **desktop**, tức chạm vào cửa sổ của mọi ứng dụng đang chạy. Chỉ cần một cửa sổ không phản hồi là cả server nghẽn, rồi poison detection báo session hỏng.
- **Sửa:** thêm `NativeWindows.GetProcessWindows(pid)` — `EnumWindows` lọc theo PID rồi mới `Automation.FromHandle`. Chỉ chạm cửa sổ của process đích. `GetMainWindow` chỉ còn là phương án cuối khi Win32 không thấy cửa sổ nào.
- **Kiểm chứng:** `LiveUiWorkflowTests` fail trước khi sửa và pass sau khi sửa, **trong khi 3 cửa sổ treo vẫn còn nguyên trên desktop**.

### 🧪 Kiểm thử — lấp các khoảng trống theo `plan.md`

- **`ProtocolSmokeTests` (mới, §13.2):** nói chuyện JSON-RPC thật với exe đã publish qua stdio — `initialize` → `tools/list` → `tools/call wf_ping`; kiểm tra mọi tool có prefix `wf_` và có `[Description]`. Không cần desktop session. Đã xác nhận test **fail đúng** khi cố tình thêm một `Console.WriteLine` vào `Program.cs` (báo "stdout KHÔNG phải JSON-RPC hợp lệ"). Hỗ trợ biến `WFVERIFY_SERVER_EXE` để CI trỏ tới thư mục publish riêng.
- **`InteractionCoverageTests` (mới):** phủ 6 tool trước đây **không có fixture nào để chạy thật** — `toggle`, `select`, `expand`, `send_keys`, `focus`, `scroll_into_view`.
- **`CatalogForm` (fixture mới):** `TreeView` (expand + select node con), `ListBox` 60 mục (select theo tên, scroll_into_view mục cuối), `CheckBox`, `DateTimePicker`, `TextBox`. Mở từ menu `File > Đơn hàng` — trước đây là menu item chết, chưa nối handler.
- Tổng: **27 test pass** (17 unit + 10 integration).

### 📚 Tài liệu

- **`plan.md` §8.4 đính chính:** code mẫu và ghi chú vẫn đang dạy `new ImageContentBlock { Data = bytes }` — đúng cái lỗi đã gây ra `Invalid Base64 string`. Đã đổi sang `ImageContentBlock.FromBytes()` kèm cảnh báo.
- `plan.md` §6.2/§11 cập nhật `wf_detach_app`, `environment` của `wf_launch_app`, `waitForWindowMs` của `wf_attach_app`, và hành vi mới của `wf_close_app`. §16 quyết định #2 (ngôn ngữ output) ghi nhận là đã chốt ngược với khuyến nghị ban đầu.
- **README** bổ sung theo GĐ 5: ràng buộc môi trường (interactive session, màn khoá, elevated/UIPI, DPI, bitness, control third-party), lưu ý bảo mật (ảnh chụp lộ dữ liệu + `WFVERIFY_DISABLE_SCREENSHOT`, phạm vi `WFVERIFY_ALLOWED_ROOTS`), và mục xử lý sự cố.
- Sửa số rule: thực tế có **15** rule `WF001`–`WF060`, README và `CLAUDE.md` ghi nhầm 14.

---

## [1.1.0] - 2026-08-21

### ✨ Hỗ trợ ứng dụng cần đăng nhập / chọn SQL thủ công

Với các dự án không thể tự khởi chạy (phải đăng nhập, chọn cơ sở dữ liệu, switch môi trường), quy trình đúng là **người dùng tự mở và chuẩn bị app → agent attach vào**. Bản này gỡ các rào cản của quy trình đó.

#### 🐞 `wf_close_app` giết cả ứng dụng không do server khởi chạy (nghiêm trọng)
- **Đã kiểm chứng bằng test thật:** mở SampleApp bằng tay (PID 4092) → `wf_attach_app` → `wf_close_app` → tiến trình bị giết. Tức là chỉ cần agent lỡ gọi `wf_close_app` là mất sạch phiên đăng nhập/kết nối CSDL đã dựng thủ công.
- **Nguyên nhân:** tool gọi `Close()`/`Kill()` mà không kiểm tra `LaunchedByUs`, trái với chính Rule 6 trong `AGENTS.md`. (`UiSession.Dispose()` có kiểm tra, riêng tool thì không.)
- **Sửa:** `wf_close_app` từ chối với `PATH_DENIED` khi `LaunchedByUs == false`, kèm hint chuyển sang `wf_detach_app`.

#### ➕ `wf_detach_app` (tool mới, tổng số tool: 27)
Rời session mà không đụng tới tiến trình. Cảnh báo nếu detach khỏi app do chính server khởi chạy (sẽ không còn được tự dọn dẹp).

#### ⬆️ `wf_attach_app` gia cố
- Thêm `waitForWindowMs` (mặc định 0) + retry, để attach được vào ứng dụng đang khởi động.
- Tìm theo `windowTitle` nay quét **mọi cửa sổ đang hiển thị** qua Win32 `EnumWindows` thay vì chỉ `Process.MainWindowTitle` — app đang đứng ở form đăng nhập / splash / dialog chọn CSDL thường có `MainWindowTitle` rỗng nên trước đây không tìm ra.
- Kết quả trả về thêm danh sách tiêu đề cửa sổ và ghi chú về `launchedByUs`.

#### ⬆️ `wf_launch_app` nhận biến môi trường
Tham số `environment` dạng `["TEN=GIA_TRI", ...]` (`ProcessStartInfo.Environment`), để đổi chuỗi kết nối SQL / cờ môi trường lúc khởi chạy mà không phải sửa file config.

### 🧪 Kiểm thử
- `AttachLifecycleTests`: `wf_close_app` phải từ chối và tiến trình phải còn sống; `wf_detach_app` giải phóng session mà app vẫn chạy; `NativeWindows.FindProcessesByWindowTitle` tìm được cửa sổ đăng nhập. Đã xác nhận test **fail** khi tạm gỡ guard.
- Tổng: **25 test pass** (17 unit + 8 integration).

---

## [1.0.1] - 2026-08-21

### 🐞 Sửa lỗi phát hiện qua kiểm thử thực tế

#### 1. `wf_get_ui_tree` / `wf_find_elements` chết trên form có MenuStrip (nặng)
- **Triệu chứng:** `PropertyNotSupportedException: The requested property 'AutomationId [#30011]' is not supported` ở mọi `maxDepth`.
- **Nguyên nhân:** shortcut của FlaUI (`element.AutomationId`, `.Name`, `.IsOffscreen`, ...) trỏ vào `Properties.X.Value`, và getter này **ném exception** khi provider không cung cấp property. `ToolStripMenuItem` của WinForms không có `AutomationId`.
- **Sửa:** thêm [`Infrastructure/UiaSafe.cs`](src/WinFormsVerifier.McpServer/Infrastructure/UiaSafe.cs) (đọc qua `ValueOrDefault` + try/catch) và thay toàn bộ shortcut trong `TreeSerializer`, `ElementDto`, `ElementLocator`, `InteractionService`, `ScreenshotService`, `WaitTools`, `AppLifecycleTools`. `SuggestCandidates` guard theo từng element để một node lỗi không làm hỏng cả danh sách gợi ý.
- **Kèm theo:** `ElementLocator.ResolveAll` trước đây `DistinctBy(NativeWindowHandle)` — các control không có HWND riêng (ToolStrip item, ô DataGridView) dùng chung handle của container nên bị gộp nhầm thành một. Đổi sang khóa định danh `RuntimeId`.

#### 2. `wf_screenshot` trả "Invalid Base64 string"
- **Nguyên nhân:** trong MCP SDK 2.2.0, `ImageContentBlock.Data` là **base64 đã encode dạng UTF-8 bytes**, không phải bytes ảnh gốc (xác minh trong `ModelContextProtocol.Core.xml`). Code gán thẳng bytes PNG/JPEG vào `Data`.
- **Sửa:** dùng `ImageContentBlock.FromBytes(bytes, mimeType)` trong [`VisualTools.cs`](src/WinFormsVerifier.McpServer/Tools/VisualTools.cs). Đính chính lại `plan.md` và `.agents/rules/mcp-stdio-rules.md` (cả hai đều ghi sai điểm này).

#### 3. `wf_dialog_respond` trả `WINDOW_NOT_FOUND` sau `wf_menu_click`
- **Chưa tái hiện được.** `MenuModalWorkflowTests` dựng lại đúng kịch bản (menu "Trợ giúp > Giới thiệu" của SampleApp mở `MessageBox`) và pass 4/4 lần chạy.
- **Đã làm:** `DialogRespond` dò lặp trong 2000ms thay vì dò một lần (xử lý trường hợp dialog chưa kịp hiện), và khi thực sự không có dialog thì thông báo nêu rõ khả năng dialog đã đóng trước đó thay vì chỉ nói "không có dialog".

### 🐞 Sửa thêm — phát hiện khi chạy thật qua MCP client

#### 4. `wf_dialog_respond` báo thành công dù dialog chưa đóng
- **Quan sát thực tế:** phải gọi 2 lần mới đóng được MessageBox "Lỗi đăng nhập"; lần đầu tool vẫn trả `ok: true`.
- **Nguyên nhân:** sau `SendMessage(BM_CLICK)` code `Thread.Sleep(300)` rồi trả thành công **mà không kiểm tra dialog có đóng thật không**.
- **Sửa:** thêm `WaitUntilDialogClosed` / `WaitUntilNoModal`. Nếu BM_CLICK không ăn thì thử lại bằng `InvokePattern` trên chính nút đó; vẫn không đóng được thì trả lỗi kèm danh sách nút thay vì báo thành công giả.

#### 5. `wf_set_value` cảnh báo sai trên ô mật khẩu + gõ phím có thể rơi vào hư không
- Ô có `PasswordChar` khi đọc lại luôn trả `Access denied`, khiến `verify` luôn báo "giá trị không khớp". Nay nhận diện ô mật khẩu và nói rõ là không thể xác thực, đồng thời che giá trị trong message.
- Đường fallback bàn phím trước đây chỉ gọi `element.Focus()` rồi gõ. Nếu cửa sổ không ở foreground, `Keyboard.Type()` gõ vào nơi khác mà không báo lỗi — tool vẫn báo "đã nhập". Nay `FocusForTyping` đưa cửa sổ lên foreground, thử `Focus()` rồi `FocusNative()`, và **ném lỗi** nếu control không thực sự nhận keyboard focus.
- **Chưa xác định được nguyên nhân** của một lần đăng nhập thất bại quan sát được khi test thật (lần 2 thành công). Giả thuyết "UIA chặn ValuePattern trên ô mật khẩu" đã bị bác bỏ bằng test: `ValuePattern.SetValue` vẫn có tác dụng. Các thay đổi trên là gia cố, không phải bản vá cho một nguyên nhân đã chứng minh.

### 🧪 Kiểm thử
- Thêm `ImageContentBlockTests` (3 test) — có một test khẳng định cách gán cũ **không** phải base64 hợp lệ.
- `LiveUiWorkflowTests` mở rộng: duyệt cây MainForm (`maxDepth` 2 và 6), dựng `ElementDto` cho mọi descendant, `ResolveAll` trên menu item. Đã xác nhận test này **fail đúng lỗi gốc** khi tạm gỡ fix.
- Thêm `MenuModalWorkflowTests`.
- Thêm `AssemblyInfo.cs` tắt chạy song song cho integration test: hai class live UI chạy song song làm UIA trả `E_FAIL`.
- Thêm `SetValue_OnPasswordField_ActuallyDeliversTheText`.
- Tổng: **23 test pass** (17 unit + 6 integration).

### ⚠️ Ghi chú kiến trúc
- `InteractionService.Invoke` cố ý dùng input phi chặn (`PostMessage` / `mouse_event`) **trước** rồi mới tới UIA pattern. `InvokePattern.Invoke()` chạy đồng bộ nên treo luồng STA nếu handler mở `MessageBox`. `.agents/rules/ui-automation-rules.md` trước đây ghi ngược thứ tự này — đã sửa lại kèm cảnh báo.

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
