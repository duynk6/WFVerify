# WinForms Verifier MCP Server — Implementation Plan (v2)

> Kế hoạch triển khai chi tiết, thay thế `docs/WinForms_Verifier_MCP_Implementation_Guide.md`.
> Mọi API trong tài liệu này đã được **verify bằng probe project compile + chạy thật** trên máy dev
> (.NET SDK 8.0.202, Windows 11). Xem §0 và §13.2 để biết cách tái lập.

---

## 0. Trạng thái & Kết quả kiểm chứng

Repo hiện tại chỉ có `docs/`. Chưa có code.

Các giả định của guide v1 đã được kiểm chứng lại bằng probe thật:

| Hạng mục | Guide v1 | Thực tế (đã probe) |
| :--- | :--- | :--- |
| Package MCP | `ModelContextProtocol 1.0.0` | **`2.2.0`** (GA). Chạy tốt trên `net8.0-windows`. |
| Bootstrap | `WebApplication` + `app.MapMcpTool()` + `RunMcpServerAsync()` | **Không tồn tại.** Đúng là `Host.CreateApplicationBuilder` + `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` |
| Khai báo tool | delegate inline | Attribute `[McpServerToolType]` / `[McpServerTool]` + `[Description]` |
| Inject service vào tool | không có | ✅ Tham số kiểu service được DI resolve tự động, **không** lọt vào `inputSchema` |
| Trả ảnh | `string` base64 | `CallToolResult` chứa `ImageContentBlock`. ⚠️ ĐÍNH CHÍNH (2026-08-21): `Data` là `ReadOnlyMemory<byte>` nhưng chứa **base64 đã encode dạng UTF-8 bytes**, KHÔNG phải raw bytes. Phải dùng `ImageContentBlock.FromBytes(bytes, mimeType)`. |
| `element.Capture()` | `image.Save(ms, ...)` | Trả `System.Drawing.Bitmap` — OK, nhưng phải downscale trước khi trả |
| FlaUI | `4.0.0` | **`5.0.0`**; toàn bộ API dùng trong plan này đã compile OK |
| Log | không đề cập | **Bắt buộc** đẩy về stderr, nếu không vỡ JSON-RPC |
| Launch server | `dotnet run --project` | Hỏng: build output ra stdout → phải publish exe |

Handshake `initialize` → `tools/list` → `tools/call` đã chạy thành công end-to-end với skeleton ở §8.

---

## 1. Mục tiêu & Phạm vi

### 1.1. Mục tiêu
Cung cấp cho AI agent (Claude Code / Desktop / Cursor) khả năng:
1. **Thanh tra runtime** cây UI của một ứng dụng WinForms đang chạy.
2. **Tương tác** với control một cách tin cậy — có wait/retry, không flaky.
3. **Thẩm định trực quan** bằng screenshot đưa vào Vision.
4. **Phân tích tĩnh** file Designer để bắt lỗi layout/binding trước khi chạy.

### 1.2. Trong phạm vi
- Ứng dụng WinForms .NET Framework 4.x và .NET 6/8/9 (Windows Desktop).
- Control chuẩn WinForms + `DataGridView`, `MenuStrip`, `ToolStrip`, `TabControl`, `TreeView`, `ListView`.
- Chạy trên một **interactive desktop session** ở máy local.

### 1.3. Ngoài phạm vi (ghi rõ để không kỳ vọng sai)
- Control third-party vẽ custom (DevExpress, Telerik, Infragistics) — chỉ **best-effort** qua `LegacyIAccessible`, không cam kết.
- WPF / WinUI / MAUI.
- Chạy headless hoặc trong CI không có session (xem §2).
- Record & replay kịch bản.
- Inject/eval code trong process đích.

---

## 2. Ràng buộc môi trường (BẮT BUỘC đọc trước)

| Ràng buộc | Chi tiết |
| :--- | :--- |
| **Session** | UI Automation cần **interactive desktop session đang mở khoá**. Không chạy được qua SSH, Windows Service, hay CI agent chạy nền. Muốn dùng trong CI → phải có RDP session giữ mở hoặc self-hosted runner ở chế độ interactive. |
| **Màn khoá** | Khi Windows lock screen, click sẽ fail và screenshot trả ảnh đen. |
| **DPI** | Server **phải** per-monitor DPI aware v2, nếu không `BoundingRectangle` và ảnh chụp sẽ lệch trên máy scale 125/150%. Xem §9. |
| **Bitness** | Build server x64. Attach app 32-bit vẫn OK qua UIA3 (cross-bitness) nhưng cần test. |
| **Quyền** | App đích chạy elevated thì server cũng phải elevated, nếu không UIA bị UIPI chặn. |
| **SDK** | Máy dev hiện có .NET SDK **8.0.202** → target `net8.0-windows`. |

---

## 3. Kiến trúc

```
+---------------------------------------------------------------+
|                  Claude Code (MCP Client)                     |
+---------------------------------------------------------------+
                    | JSON-RPC over stdio (stdout = protocol ONLY)
                    | stderr = logs
                    v
+---------------------------------------------------------------+
|        WinFormsVerifier.McpServer  (net8.0-windows, x64)      |
|                                                               |
|  Tools layer  [McpServerToolType] — mỏng: validate + map       |
|  ----------------------------------------------------------   |
|  Services layer                                               |
|  +-------------------+  +-----------------------------------+ |
|  | RoslynAnalyzer    |  | UiSession (singleton)             | |
|  | (static, không    |  |  - chạy trên 1 STA thread duy nhất| |
|  |  đụng UIA)        |  |  - giữ Application + UIA3Automation| |
|  +-------------------+  |  - ElementLocator / TreeSerializer | |
|                         |  - InteractionService / Screenshot | |
|                         +-----------------------------------+ |
+---------------------------------------------------------------+
        |                                    |
        v                                    v
 [ *.cs / *.Designer.cs ]           [ WinForms process đang chạy ]
```

**Bốn nguyên tắc kiến trúc:**
1. **Mọi lời gọi UIA đi qua đúng một STA thread.** UIA3 là COM, không thread-safe; MCP có thể gọi tool đồng thời.
2. **Tools layer mỏng.** Logic automation nằm ở Services → unit-test được.
3. **Mọi tool đều có timeout cứng.** Một lệnh treo = chết cả MCP session.
4. **stdout là thánh địa.** Không `Console.WriteLine` ở bất kỳ đâu.

---

## 4. Cấu trúc thư mục

```
WFVerify/
├── plan.md
├── .mcp.json
├── WinFormsVerifier.sln
├── src/
│   └── WinFormsVerifier.McpServer/
│       ├── WinFormsVerifier.McpServer.csproj
│       ├── app.manifest                    # DPI awareness (§9)
│       ├── Program.cs                      # bootstrap Host + DI, ~30 dòng
│       ├── Tools/
│       │   ├── DiagnosticsTools.cs         # wf_ping
│       │   ├── AppLifecycleTools.cs        # launch / attach / list_windows / close
│       │   ├── UiInspectionTools.cs        # get_ui_tree / find_elements / get_element
│       │   ├── UiInteractionTools.cs       # invoke / set_value / toggle / select / keys
│       │   ├── WaitTools.cs                # wait_for / wait_idle
│       │   ├── VisualTools.cs              # screenshot
│       │   └── StaticAnalysisTools.cs      # analyze_form / analyze_project / list_rules
│       ├── Services/
│       │   ├── UiThread.cs                 # STA message pump + timeout
│       │   ├── UiSession.cs                # state: Application, Automation, windows
│       │   ├── ElementLocator.cs           # selector resolution + fallback + fuzzy
│       │   ├── TreeSerializer.cs           # cây UI → text compact
│       │   ├── ScreenshotService.cs        # capture + downscale + encode
│       │   ├── InteractionService.cs       # pattern fallback chain
│       │   └── Roslyn/
│       │       ├── FormAnalyzer.cs         # dựng Compilation từ cụm partial file
│       │       ├── DesignerModel.cs        # cây control parse từ InitializeComponent
│       │       └── Rules/                  # mỗi rule 1 file (§10)
│       ├── Models/
│       │   ├── Selector.cs
│       │   ├── ElementDto.cs
│       │   ├── ToolResult.cs               # envelope ok/error
│       │   └── Diagnostic.cs
│       └── Infrastructure/
│           ├── PathGuard.cs                # whitelist đường dẫn (§11)
│           ├── ToolException.cs
│           └── McpResults.cs               # helper build CallToolResult
├── tests/
│   ├── WinFormsVerifier.UnitTests/         # Roslyn rules, selector, serializer
│   └── WinFormsVerifier.IntegrationTests/  # drive SampleApp qua UiSession
└── samples/
    └── SampleApp/                          # WinForms fixture (§13.1)
```

---

## 5. Contracts — thiết kế trước, code sau

### 5.1. Selector model — thay cho `automationId` trần

Đây là sửa lỗi thiết kế lớn nhất so với v1. Rất nhiều control WinForms **không có AutomationId ổn định**:
Label, Panel, GroupBox, cell của DataGridView, item của MenuStrip. v1 giả định `automationId` luôn tồn tại
ở cả 3 tool tương tác → sẽ vỡ ngay trên app thật.

Selector là một **string** dạng `prefix:value`, nối bằng ` > ` để mô tả quan hệ cha–con:

```
id:txtUsername                       # AutomationId (ưu tiên cao nhất)
name:Đăng nhập                       # UIA Name / text hiển thị
name~:Đăng                           # Name chứa chuỗi (contains, không phân biệt hoa thường)
type:Button                          # ControlType
class:WindowsForms10.EDIT.app.0.xxx  # ClassName
help:Nhập mã nhân viên               # HelpText (= Control.AccessibleDescription)
idx:3                                # chỉ số trong các anh em cùng cấp
grid:2,5                             # ô (row,col) qua Grid pattern

# Kết hợp:
id:dgOrders > grid:0,2               # cell dòng 0 cột 2 của DataGridView
type:Menu > name:File > name:Thoát   # menu item lồng nhau
name~:Khách hàng > type:Button > idx:0
```

**Fallback chain khi resolve** (`ElementLocator`):
1. Thử `id:` exact.
2. Miss → `name:` exact.
3. Miss → `name~:` contains.
4. Miss → `help:`.
5. Vẫn miss → **trả lỗi kèm 10 ứng viên gần nhất** (fuzzy Levenshtein trên `Name` + `AutomationId`)
   để agent tự sửa selector mà không phải gọi lại `wf_get_ui_tree`.

Bước 5 là chi tiết UX quan trọng: nó cắt được một vòng round-trip mỗi lần agent đoán sai tên control —
tình huống xảy ra rất thường xuyên.

### 5.2. Result envelope

Mọi tool trả **JSON string** theo envelope thống nhất, và set `IsError` của `CallToolResult`
khi thất bại để client phân biệt được lỗi với kết quả:

```jsonc
// thành công
{ "ok": true, "data": { /* ... */ }, "warnings": ["Cây bị cắt ở maxDepth=5"] }

// thất bại
{
  "ok": false,
  "error": {
    "code": "ELEMENT_NOT_FOUND",
    "message": "No element matched 'id:btnLogn'",
    "hint": "Có phải bạn muốn 'btnLogin'?",
    "candidates": [ { "selector": "id:btnLogin", "name": "Đăng nhập", "type": "Button" } ]
  }
}
```

**Bảng mã lỗi:**

| Code | Ý nghĩa |
| :--- | :--- |
| `NO_SESSION` | Chưa launch/attach app |
| `APP_EXITED` | Process đã thoát |
| `WINDOW_NOT_FOUND` | Không có cửa sổ khớp |
| `ELEMENT_NOT_FOUND` | Selector không resolve được (kèm `candidates`) |
| `AMBIGUOUS` | Selector khớp nhiều element (kèm danh sách) |
| `PATTERN_UNSUPPORTED` | Control không hỗ trợ thao tác yêu cầu (kèm danh sách pattern có sẵn) |
| `TIMEOUT` | Quá thời gian chờ (kèm hướng dẫn recover) |
| `BLOCKED_BY_MODAL` | Có modal dialog đang chặn — **kèm text của dialog** |
| `PATH_DENIED` | Đường dẫn ngoài whitelist (§11) |
| `INTERNAL` | Lỗi không lường trước (kèm exception type, không kèm stacktrace) |

> `BLOCKED_BY_MODAL` là mã quan trọng nhất trong thực tế. App WinForms nghiệp vụ liên tục bật
> `MessageBox`. Khi bất kỳ tool tương tác nào phát hiện modal window mới, phải trả mã này kèm
> nội dung dialog — thay vì treo, hoặc báo `ELEMENT_NOT_FOUND` gây hiểu nhầm hoàn toàn.

---

## 6. Danh mục MCP Tools

Prefix `wf_` cho mọi tool để tránh đụng tên với MCP server khác.

### 6.1. Diagnostics
| Tool | Params | Mô tả |
| :--- | :--- | :--- |
| `wf_ping` | — | Health check: version server, trạng thái session, DPI scale hiện tại. |

### 6.2. App lifecycle
| Tool | Params | Mô tả |
| :--- | :--- | :--- |
| `wf_launch_app` | `exePath`, `arguments?`, `environment?`, `workingDir?`, `waitForWindowMs=15000` | Khởi chạy và chờ main window sẵn sàng. `environment` dạng `["TEN=GIA_TRI"]` để đổi chuỗi kết nối SQL / cờ môi trường mà không sửa config. |
| `wf_attach_app` | `processId?`, `processName?`, `windowTitle?`, `waitForWindowMs=0` | Attach. `windowTitle` so khớp **mọi cửa sổ đang hiển thị** (Win32 `EnumWindows`) chứ không chỉ `MainWindowTitle`, để bắt được app đang đứng ở form đăng nhập / dialog chọn CSDL. Nhiều ứng viên → `AMBIGUOUS` kèm danh sách PID. |
| `wf_list_windows` | `includeChildren=false` | Liệt kê **mọi** top-level window + modal của process (title, handle, isModal, isActive). |
| `wf_detach_app` | — | Rời session mà **không** đụng tới tiến trình. Cách đúng để kết thúc phiên với app được attach. |
| `wf_close_app` | `force=false`, `timeoutMs=5000` | `Close()` → chờ → `Kill()` nếu vẫn sống. **Chỉ áp dụng cho tiến trình do server khởi chạy** (`LaunchedByUs == true`); attach vào app người dùng tự mở thì từ chối với `PATH_DENIED` và chỉ sang `wf_detach_app`. Xem §11. |

### 6.3. Inspection
| Tool | Params | Mô tả |
| :--- | :--- | :--- |
| `wf_get_ui_tree` | `windowSelector?`, `maxDepth=5`, `filterTypes?`, `includeInvisible=false`, `maxNodes=300` | Cây UI thật, format text thụt lề compact. |
| `wf_find_elements` | `selector`, `windowSelector?`, `limit=20` | Tìm nhiều element khớp. |
| `wf_get_element` | `selector`, `windowSelector?` | Chi tiết 1 element + **danh sách pattern khả dụng** (để agent biết thao tác nào hợp lệ). |

### 6.4. Interaction
| Tool | Params | Mô tả |
| :--- | :--- | :--- |
| `wf_invoke` | `selector`, `windowSelector?` | Click/kích hoạt qua fallback chain (§7.5). |
| `wf_set_value` | `selector`, `value`, `mode=replace\|append`, `verify=true` | Set text; `verify` đọc lại giá trị sau khi set. |
| `wf_toggle` | `selector`, `state=on\|off\|toggle` | CheckBox / RadioButton. |
| `wf_select` | `selector`, `item?`, `index?` | ComboBox / ListBox / ListView / TabControl / TreeView. |
| `wf_expand` | `selector`, `expand=true` | TreeView node, ComboBox dropdown. |
| `wf_send_keys` | `keys`, `selector?` | Gõ phím thô (`^s`, `{ENTER}`, `%{F4}`). Focus trước nếu có selector. |
| `wf_focus` | `selector` | Đặt focus. |
| `wf_scroll_into_view` | `selector` | ScrollItem pattern. |
| `wf_grid_read` | `selector`, `startRow=0`, `maxRows=50`, `maxCols=20` | Đọc DataGridView thành bảng text — **thay vì bắt agent đọc từ ảnh**. |
| `wf_grid_set_cell` | `selector`, `row`, `col`, `value` | Sửa ô grid. |
| `wf_menu_click` | `path` (vd `File>Mở>Gần đây`) | MenuStrip/ContextMenuStrip, tự expand từng cấp. |
| `wf_dialog_respond` | `button` (`OK\|Cancel\|Yes\|No\|...`) | Trả lời modal dialog đang chặn. |

### 6.5. Synchronization
| Tool | Params | Mô tả |
| :--- | :--- | :--- |
| `wf_wait_for` | `selector`, `state=exists\|visible\|enabled\|gone`, `timeoutMs=10000` | Chờ có điều kiện. |
| `wf_wait_idle` | `timeoutMs=5000` | `WaitWhileBusy` — chờ app xử lý xong. |

### 6.6. Visual
| Tool | Params | Mô tả |
| :--- | :--- | :--- |
| `wf_screenshot` | `selector?`, `windowSelector?`, `maxWidth=1200`, `format=png\|jpeg`, `quality=80` | Trả `ImageContentBlock`. Bắt buộc downscale (§7.6). |

### 6.7. Static analysis
| Tool | Params | Mô tả |
| :--- | :--- | :--- |
| `wf_analyze_form` | `formPath` (`Form1.cs` **hoặc** `Form1.Designer.cs`), `rules?`, `minSeverity=info` | Phân tích cả cụm partial class. |
| `wf_analyze_project` | `projectPath` (`.csproj`), `minSeverity=warning`, `maxForms=50` | Quét toàn bộ form trong project. |
| `wf_list_rules` | — | Liệt kê rule + severity mặc định (§10.3). |

---

## 7. Core components — thiết kế chi tiết

### 7.1. `UiThread` — STA message pump

Vấn đề: UIA3 là COM không thread-safe; MCP có thể gọi tool song song; FlaUI giữ state ngầm.
Giải pháp: **một** thread STA duy nhất, mọi công việc UIA xếp hàng qua đó.

```csharp
public sealed class UiThread : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private int _consecutiveTimeouts;

    public UiThread()
    {
        _thread = new Thread(Pump) { IsBackground = true, Name = "UIA-STA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    private void Pump()
    {
        foreach (var work in _queue.GetConsumingEnumerable())
            try { work(); } catch { /* đã capture vào TCS trong RunAsync */ }
    }

    /// <summary>Chạy công việc UIA trên STA thread, có timeout cứng.</summary>
    public async Task<T> RunAsync<T>(Func<T> work, TimeSpan timeout, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try { tcs.TrySetResult(work()); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        }, ct);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        var done = await Task.WhenAny(tcs.Task, Task.Delay(Timeout.Infinite, cts.Token));

        if (done != tcs.Task)
        {
            if (Interlocked.Increment(ref _consecutiveTimeouts) >= 2)
                throw new ToolException(ErrorCode.Timeout,
                    $"Thao tác vượt quá {timeout.TotalSeconds:0.#}s lần thứ 2 liên tiếp. " +
                    "Session UI có thể đã hỏng — hãy gọi wf_close_app rồi attach lại.");
            throw new ToolException(ErrorCode.Timeout, $"Thao tác vượt quá {timeout.TotalSeconds:0.#}s");
        }

        Interlocked.Exchange(ref _consecutiveTimeouts, 0);
        return await tcs.Task;
    }

    public void Dispose() { _queue.CompleteAdding(); _thread.Join(2000); }
}
```

> **Lưu ý:** timeout chỉ bỏ *chờ*, công việc vẫn chạy tiếp trên STA thread và có thể chặn hàng đợi.
> Vì vậy cần cơ chế "poison" ở trên: 2 lần timeout liên tiếp → coi session hỏng và hướng dẫn agent
> recover ngay trong message lỗi.

### 7.2. `UiSession` — state singleton

Giữ: `Application? App`, `UIA3Automation Automation`, `Window? CachedMainWindow`, `bool LaunchedByUs`.

Trách nhiệm:
- `EnsureAlive()` → ném `NO_SESSION` / `APP_EXITED` sớm với message rõ ràng.
- `ResolveWindow(selector?)` → khi `selector` null, ưu tiên: **modal window đang active** > main window.
  Hành vi này quan trọng: khi có `MessageBox`, agent phải "nhìn thấy" dialog đó trước tiên.
- `DetectBlockingModal()` → gọi trước mọi thao tác tương tác; nếu có modal khác window đích
  → trả `BLOCKED_BY_MODAL` kèm text dialog.
- `IDisposable` → dispose `UIA3Automation`, và **kill process nếu do ta launch** (tránh process mồ côi
  khi Claude Code restart). Đăng ký qua `IHostApplicationLifetime.ApplicationStopping` + `AppDomain.ProcessExit`.

v1 không dispose `UIA3Automation` và không có shutdown hook → mỗi lần client restart để lại một
process app mồ côi.

### 7.3. `ElementLocator`

```csharp
public sealed class ElementLocator
{
    public AutomationElement Resolve(AutomationElement scope, string selector, TimeSpan timeout);
    public IReadOnlyList<AutomationElement> ResolveAll(AutomationElement scope, string selector, int limit);
    public IReadOnlyList<ElementDto> SuggestCandidates(AutomationElement scope, string selector, int take = 10);
}
```

Chi tiết triển khai:
- Mỗi bước dùng `Retry.WhileNull(() => ..., timeout)` của FlaUI thay vì tìm một lần → hết flaky.
- **Scope hẹp dần**: bước sau chỉ tìm trong kết quả bước trước → nhanh hơn `FindAllDescendants` toàn cây rất nhiều.
- Cache `ConditionFactory` theo automation instance.
- `grid:r,c` → `Patterns.Grid.GetItem(row, col)`.

### 7.4. `TreeSerializer` — chống nổ context

Sửa lỗi lớn thứ hai của v1: `FindAllDescendants()` + `WriteIndented = true` trên một form nghiệp vụ
dễ sinh vài trăm KB JSON, và tên tool là "hierarchy" nhưng kết quả lại là danh sách phẳng.
Tham số `maxDepth`/`windowTitle` khai báo trong bảng tool của v1 cũng không hề được dùng trong code.

Chiến lược:
- Duyệt bằng `automation.TreeWalkerFactory.GetControlViewWalker()` (control view đã lọc sẵn element trang trí, không dùng raw view).
- Lọc mặc định: bỏ element `IsOffscreen`; bỏ element có `Name` rỗng **và** `AutomationId` rỗng **và** không có pattern tương tác nào.
- Format text thụt lề thay vì JSON — tiết kiệm ~55% token:

```
Window "Quản lý đơn hàng" [1240x780]
  Pane
    Edit      id=txtMaKH   name="Mã KH"  val="KH001"   @12,40 180x23
    Button    id=btnTim    name="Tìm"                  @200,40 75x23
    DataGrid  id=dgOrders  rows=42 cols=6              @12,80 1200x600
    Button    id=btnLuu    name="Lưu"    DISABLED      @12,700 75x23
```

- Khi chạm `maxNodes` hoặc `maxDepth` → **không im lặng cắt**, mà thêm dòng
  `... (còn 84 element — tăng maxDepth hoặc dùng wf_find_elements)` và đẩy vào `warnings`.

### 7.5. `InteractionService` — pattern fallback chain

v1 dùng `element.AsButton().Click()` → ném exception với LinkLabel, ToolStripButton, ListViewItem;
và `AsTextBox().Text = value` ném khi control read-only hoặc không hỗ trợ ValuePattern.
Thay bằng chain có thứ tự, dừng ở bước đầu tiên thành công:

| Thao tác | Chain |
| :--- | :--- |
| invoke | `Patterns.Invoke` → `Patterns.SelectionItem.Select` → `Patterns.LegacyIAccessible.DoDefaultAction` → `element.Click()` (physical) |
| set_value | `Patterns.Value.SetValue` (kiểm `IsReadOnly` trước) → `Focus()` + SendKeys (Ctrl+A rồi gõ đè) |
| toggle | `Patterns.Toggle` → `Patterns.SelectionItem` → invoke chain |
| select | `Patterns.SelectionItem.Select` → `Patterns.ExpandCollapse` + tìm con + Select |

Nếu hết chain → `PATTERN_UNSUPPORTED` **kèm danh sách pattern element thực sự có**, để agent chọn tool khác.

Sau mỗi thao tác:
1. `WaitWhileBusy(500ms)`.
2. Kiểm tra modal mới xuất hiện → nếu có, trả **thành công kèm warning chứa nội dung dialog**.
3. Nếu `verify=true` → đọc lại state, so sánh, báo `warnings` nếu không khớp.

Bước 2 là thứ v1 thiếu hoàn toàn và là nguyên nhân số một khiến agent đi lạc: click nút Lưu,
app bật `MessageBox("Thiếu mã KH")`, agent không biết và tiếp tục thao tác lên form đã bị chặn.

### 7.6. `ScreenshotService`

```
Capture (Bitmap)
  → nếu width > maxWidth: resize giữ tỉ lệ (InterpolationMode.HighQualityBicubic)
  → encode PNG (mặc định), hoặc JPEG q80 nếu ảnh lớn
  → nếu bytes > 4MB: hạ maxWidth xuống 800, encode lại, thêm warning
  → CallToolResult { TextContentBlock mô tả + ImageContentBlock }
```

`TextContentBlock` đi kèm nêu kích thước gốc, cửa sổ nào, đã scale bao nhiêu — giúp Vision biết ảnh
đã bị thu nhỏ, tránh nhận xét sai về kích thước font/control.

---

## 8. Code skeleton (đã verify compile & chạy thật)

### 8.1. `WinFormsVerifier.McpServer.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <PlatformTarget>x64</PlatformTarget>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <InvariantGlobalization>false</InvariantGlobalization>
    <!-- KHÔNG cần UseWindowsForms: server không host form nào -->
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="2.2.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
    <PackageReference Include="FlaUI.UIA3" Version="5.0.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Workspaces" Version="4.8.0" />
  </ItemGroup>
</Project>
```

> `System.Drawing.Common` đến sẵn theo `net8.0-windows` — không cần khai báo riêng.
> Roslyn pin `4.8.0` cho khớp SDK 8.0.x; **không** dùng 5.x vì nó yêu cầu MSBuild/SDK mới hơn.

### 8.2. `Program.cs` — bootstrap

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using WinFormsVerifier.Services;

var builder = Host.CreateApplicationBuilder(args);

// TỐI QUAN TRỌNG: stdout dành riêng cho JSON-RPC. Mọi log phải ra stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSingleton<UiThread>();
builder.Services.AddSingleton<UiSession>();
builder.Services.AddSingleton<ElementLocator>();
builder.Services.AddSingleton<InteractionService>();
builder.Services.AddSingleton<ScreenshotService>();
builder.Services.AddSingleton<TreeSerializer>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// Dọn dẹp: không để lại process WinForms mồ côi khi client restart
host.Services.GetRequiredService<IHostApplicationLifetime>()
    .ApplicationStopping.Register(() =>
        host.Services.GetRequiredService<UiSession>().Dispose());

await host.RunAsync();
```

### 8.3. Mẫu một tool — pattern chuẩn cho MỌI tool

```csharp
using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class UiInspectionTools
{
    [McpServerTool(Name = "wf_get_ui_tree")]
    [Description("""
        Lấy cây control của cửa sổ WinForms đang chạy, dạng text thụt lề gọn.
        Dùng tool này TRƯỚC khi tương tác để biết selector hợp lệ.
        Trả tối đa maxNodes element; nếu bị cắt sẽ có cảnh báo trong 'warnings'.
        """)]
    public static async Task<CallToolResult> GetUiTree(
        UiSession session,                    // ← DI tự resolve, không lọt vào inputSchema
        TreeSerializer serializer,
        [Description("Cửa sổ đích, vd 'name~:Đơn hàng'. Bỏ trống = modal đang active, nếu không có thì main window.")]
        string? windowSelector = null,
        [Description("Độ sâu tối đa, mặc định 5. Tăng dần nếu chưa thấy control cần tìm.")]
        int maxDepth = 5,
        [Description("Lọc theo ControlType, phân tách bằng dấu phẩy, vd 'Button,Edit,ComboBox'.")]
        string? filterTypes = null,
        int maxNodes = 300,
        CancellationToken ct = default)
    {
        return await McpResults.GuardAsync(async () =>
        {
            var result = await session.RunAsync(
                () => serializer.Serialize(session.ResolveWindow(windowSelector),
                                           maxDepth, filterTypes, maxNodes),
                TimeSpan.FromSeconds(20), ct);
            return McpResults.Ok(result.Text, result.Warnings);
        });
    }
}
```

**Quy ước bắt buộc cho mọi tool:**
- `[Description]` chi tiết ở **cả** tool lẫn từng tham số. Đây là thứ duy nhất agent dựa vào để dùng đúng —
  viết cho *agent* đọc, nêu rõ *khi nào dùng* và *dùng sau tool nào*.
- Nhận `CancellationToken`.
- Bọc trong `McpResults.GuardAsync` để map exception → envelope lỗi + `IsError = true`.
  **Không bao giờ** để exception thoát ra transport.
- Service inject qua tham số (đã verify: SDK tự resolve từ DI và ẩn khỏi schema).

### 8.4. Trả ảnh đúng cách

```csharp
[McpServerTool(Name = "wf_screenshot")]
[Description("Chụp ảnh cửa sổ hoặc control để thẩm định layout bằng Vision.")]
public static async Task<CallToolResult> Screenshot(
    UiSession session, ScreenshotService shots,
    string? selector = null, string? windowSelector = null,
    int maxWidth = 1200, string format = "png",
    CancellationToken ct = default)
{
    var shot = await session.RunAsync(
        () => shots.Capture(selector, windowSelector, maxWidth, format),
        TimeSpan.FromSeconds(20), ct);

    return new CallToolResult
    {
        Content =
        {
            new TextContentBlock { Text = shot.Describe() },  // "Window 'X' 1920x1080 → scaled 1200x675"
            ImageContentBlock.FromBytes(shot.Bytes, shot.MimeType)
        }
    };
}
```

> ⚠️ **ĐÍNH CHÍNH (2026-08-21) — đã gây lỗi thật, đừng viết lại theo kiểu cũ.**
> `ImageContentBlock.Data` đúng là `ReadOnlyMemory<byte>`, nhưng nội dung nó mong đợi là
> **base64 đã encode dưới dạng UTF-8 bytes**, KHÔNG phải bytes ảnh thô. Gán thẳng bytes PNG/JPEG
> vào `Data` khiến client từ chối với `Invalid Base64 string`.
> Luôn dùng `ImageContentBlock.FromBytes(bytes, mimeType)` — hàm này nhận bytes gốc và tự encode.
> Xem `ImageContentBlockTests` để biết bằng chứng.
>
> v1 trả `Convert.ToBase64String(...)` kiểu `string` → cũng sai, nhưng theo hướng khác.

---

## 9. DPI awareness

`app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:asm.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" /> <!-- Win 10/11 -->
    </application>
  </compatibility>
</assembly>
```

**Kiểm chứng:** `wf_ping` trả về DPI scale của primary monitor. Test thủ công ở 100% và 150% —
`BoundingRectangle` của cùng một control phải cho cùng giá trị.

---

## 10. Static analysis — thiết kế lại

### 10.1. Vì sao thiết kế v1 không khả thi

`verify_designer_code(designerFilePath)` chỉ parse SyntaxTree của **một** file `.Designer.cs`
thì **không thể** phát hiện "event handler mồ côi" — handler được khai báo ở file partial còn lại
(`Form1.cs`). Cần tối thiểu một `CSharpCompilation` gồm cả cụm partial class.

### 10.2. Cách làm

`FormAnalyzer` nhận đường dẫn bất kỳ file nào của form, rồi:
1. Suy ra cụm file cùng partial class: `Form1.cs`, `Form1.Designer.cs`, `Form1.*.cs` trong cùng thư mục.
2. Tạo `CSharpCompilation` từ các SyntaxTree đó + reference assembly WinForms
   (`System.Windows.Forms.dll`, `System.Drawing.dll` từ ref pack). **Chấp nhận compilation có lỗi** —
   chỉ cần SemanticModel đủ resolve symbol trong cụm file này.
3. Parse `InitializeComponent()` thành `DesignerModel`: cây control với các thuộc tính gán được
   (`Name`, `Text`, `Location`, `Size`, `TabIndex`, `Anchor`, `Dock`, `Visible`, `Enabled`,
   `AccessibleName`, `Font`) — chỉ lấy literal/hằng, bỏ qua giá trị runtime.
4. Chạy từng rule trên `DesignerModel` + `SemanticModel`.

`wf_analyze_project` đọc `.csproj` bằng XML thuần (**không** dùng `MSBuildWorkspace` — nó kéo theo
`Microsoft.Build.Locator` và rất hay vỡ khi lệch phiên bản SDK), gom mọi `*.Designer.cs` rồi chạy
`FormAnalyzer` cho từng form.

### 10.3. Bộ rule — implement theo thứ tự ưu tiên

| ID | Severity | Kiểm tra |
| :--- | :--- | :--- |
| `WF001` | error | Handler được gắn (`+=`) nhưng method không tồn tại trong cụm partial. |
| `WF002` | warning | Method có dạng handler (`btnX_Click`, signature `(object, EventArgs)`) nhưng **không** được gắn ở đâu → handler mồ côi thật sự. |
| `WF010` | warning | Hai control cùng cha **chồng lấn** hình chữ nhật, cả hai đều `Visible`. |
| `WF011` | warning | Control nằm **ngoài** `ClientSize` của container cha → bị cắt. |
| `WF012` | info | Control có `Location` âm. |
| `WF020` | warning | `TabIndex` **trùng** giữa các control cùng container. |
| `WF021` | info | `TabIndex` không liên tục, hoặc control tương tác thiếu `TabIndex`. |
| `WF022` | info | Thứ tự `TabIndex` không khớp thứ tự đọc trực quan (trên→dưới, trái→phải). |
| `WF030` | error | `Dock = Fill` **kết hợp** `Anchor` khác mặc định → xung đột, Anchor bị bỏ qua. |
| `WF031` | warning | Control trong container resize được nhưng `Anchor = Top,Left` → không co giãn. |
| `WF040` | warning | Control tương tác (Button/TextBox/ComboBox/DataGridView) **thiếu `AccessibleName`** và `Text` rỗng → **chính tool này sẽ không định vị được nó** ở runtime. |
| `WF041` | info | `Name` control còn ở dạng mặc định (`button1`, `textBox3`). |
| `WF050` | warning | Font hardcode khác font mặc định của form → nguy cơ vỡ layout khi đổi DPI/theme. |
| `WF051` | info | `AutoScaleMode` không phải `Dpi`/`Font` → rủi ro scale. |
| `WF060` | info | Control được khai báo nhưng **không** `Controls.Add` → dead control. |

> `WF040` là rule quan trọng nhất về mặt sản phẩm: nó nối phần static với phần runtime.
> Chạy nó trước khi automation giải thích được vì sao selector hay fail, và đưa ra hành động sửa cụ thể.

Output của `wf_analyze_form`:
```jsonc
{ "ok": true, "data": {
  "form": "MainForm", "controlCount": 47,
  "diagnostics": [
    { "rule":"WF040","severity":"warning","control":"textBox3",
      "file":"MainForm.Designer.cs","line":142,
      "message":"TextBox không có AccessibleName và Name còn mặc định — không thể định vị bằng selector.",
      "fix":"Đặt Name = 'txtSoLuong' và AccessibleName = 'Số lượng'." }
  ],
  "summary": { "error":1, "warning":6, "info":12 } } }
```

---

## 11. Bảo mật

| Rủi ro | Biện pháp |
| :--- | :--- |
| `wf_launch_app` chạy exe tuỳ ý | `PathGuard`: chỉ cho phép đường dẫn trong whitelist, đọc từ env `WFVERIFY_ALLOWED_ROOTS` (phân tách `;`), mặc định = thư mục làm việc của server. Ngoài whitelist → `PATH_DENIED`. |
| Command injection | Truyền `arguments` qua `ProcessStartInfo.ArgumentList`, **không** ghép chuỗi; `UseShellExecute = false`. |
| Đọc file tuỳ ý qua `wf_analyze_form` | Áp cùng `PathGuard`. |
| Screenshot lộ dữ liệu nhạy cảm | Ghi rõ trong README; env `WFVERIFY_DISABLE_SCREENSHOT=1` để tắt hẳn tool. |
| Kill nhầm process | `wf_close_app` chỉ tác động lên process trong session hiện tại, và **chỉ khi process đó do server khởi chạy**. App do người dùng tự mở (thường đã đăng nhập / trỏ vào một môi trường SQL cụ thể) sẽ bị từ chối — dùng `wf_detach_app`. Không có tool kill theo tên. |

---

## 12. Đóng gói & cấu hình client

### 12.1. Vì sao KHÔNG dùng `dotnet run --project`
`dotnet run` in output build/restore ra **stdout** → chèn rác vào stream JSON-RPC → client fail handshake,
và fail theo kiểu rất khó chẩn đoán (client chỉ báo "server disconnected").

### 12.2. Cách đúng
```bash
dotnet publish src/WinFormsVerifier.McpServer -c Release -r win-x64 --self-contained false -o dist
```

`.mcp.json` ở gốc project (Claude Code tự nhận, commit được vào repo):
```json
{
  "mcpServers": {
    "winforms-verifier": {
      "command": "E:/AgentTest/WFVerify/dist/WinFormsVerifier.McpServer.exe",
      "args": [],
      "env": {
        "WFVERIFY_ALLOWED_ROOTS": "E:/AgentTest;C:/Projects",
        "WFVERIFY_LOG_LEVEL": "Information"
      }
    }
  }
}
```

Hoặc: `claude mcp add winforms-verifier -- E:/AgentTest/WFVerify/dist/WinFormsVerifier.McpServer.exe`

---

## 13. Chiến lược kiểm thử

### 13.1. `samples/SampleApp` — fixture bắt buộc

Một WinForms app nhỏ, **cố tình chứa cả trường hợp tốt lẫn xấu**, làm ground truth cho integration test
lẫn demo. Không có nó thì không có cách nào verify server hoạt động đúng.

- `LoginForm`: `txtUsername`, `txtPassword` (PasswordChar), `btnLogin`, `chkRemember`.
  Sai mật khẩu → `MessageBox` → test `BLOCKED_BY_MODAL` và `wf_dialog_respond`.
- `MainForm`: `MenuStrip` (File > Đơn hàng > Thoát), `ToolStrip`, `TabControl` 3 tab.
- `OrdersForm`: `DataGridView` 50 dòng × 6 cột (test `wf_grid_read` + phân trang), `ComboBox`,
  `DateTimePicker`, `TreeView`.
- `SlowForm`: nút gây `Thread.Sleep(3000)` → test `wf_wait_idle` và timeout.
- `BadLayoutForm`: **cố tình** vi phạm WF010/WF020/WF030/WF040 → ground truth cho từng rule static.

### 13.2. Ba tầng test

| Tầng | Nội dung | Chạy được trong CI |
| :--- | :--- | :--- |
| Unit | Selector parsing, fuzzy candidate, TreeSerializer (trên cây giả), toàn bộ Roslyn rules | ✅ |
| Protocol smoke | Pipe `initialize` + `tools/list` + `tools/call wf_ping` vào exe, assert JSON trả về | ✅ |
| Integration | Launch `SampleApp` thật → chạy từng tool → assert | ❌ cần desktop session |

Protocol smoke test là lưới an toàn rẻ nhất — nó bắt lỗi "log lọt stdout" ngay lập tức.
Script dưới đây **đã chạy thành công** trên probe:

```bash
{ echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"t","version":"1"}}}'
  sleep 2
  echo '{"jsonrpc":"2.0","method":"notifications/initialized"}'
  sleep 1
  echo '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"wf_ping","arguments":{}}}'
  sleep 3
} | ./dist/WinFormsVerifier.McpServer.exe 2>server.log
```

Lưu ý: phải feed stdin có nhịp (sleep), nếu đóng stdin ngay thì server thoát trước khi kịp trả lời.

---

## 14. Roadmap

Ước lượng cho 1 dev. Chia lại so với v1: **rủi ro lớn nhất nằm ở tầng MCP/stdio và ở độ tin cậy của
selector, không phải ở FlaUI** — nên đẩy hai thứ đó lên sớm nhất.

### GĐ 0 — Walking skeleton (1–2 ngày)
- [ ] Solution + project theo §4, csproj §8.1, manifest §9.
- [ ] `Program.cs` §8.2 + tool duy nhất `wf_ping`.
- [ ] Protocol smoke test (§13.2) chạy xanh.
- [ ] Publish exe + đăng ký `.mcp.json`; Claude Code gọi được `wf_ping`.

**DoD:** Claude Code báo server connected và gọi `wf_ping` thành công.
*(Giai đoạn này đã được prototype và verify — xem §0.)*

### GĐ 1 — Session & Inspection (5–6 ngày)
- [ ] `UiThread` (STA pump + timeout + poison detection).
- [ ] `UiSession` + cleanup lifecycle (không để process mồ côi).
- [ ] `PathGuard`, `ToolException`, `McpResults`, error envelope §5.2 đầy đủ.
- [ ] `wf_launch_app`, `wf_attach_app`, `wf_list_windows`, `wf_close_app`.
- [ ] `ElementLocator` (selector §5.1 + fallback + fuzzy candidates).
- [ ] `TreeSerializer` + `wf_get_ui_tree`, `wf_find_elements`, `wf_get_element`.
- [ ] `wf_wait_for`, `wf_wait_idle`.
- [ ] `SampleApp` (LoginForm + MainForm).

**DoD:** Agent tự launch `SampleApp`, lấy cây UI dưới 300 node, và khi selector sai thì nhận được gợi ý đúng.

### GĐ 2 — Interaction (4–5 ngày)
- [ ] `InteractionService` + pattern fallback chain §7.5.
- [ ] Phát hiện modal → `BLOCKED_BY_MODAL` + `wf_dialog_respond`.
- [ ] `wf_invoke`, `wf_set_value`, `wf_toggle`, `wf_select`, `wf_expand`, `wf_focus`, `wf_send_keys`, `wf_scroll_into_view`.
- [ ] `wf_grid_read`, `wf_grid_set_cell`, `wf_menu_click`.
- [ ] `OrdersForm`, `SlowForm` + integration test cho từng tool.

**DoD:** Agent hoàn thành luồng "đăng nhập sai → đọc dialog lỗi → đóng dialog → đăng nhập đúng →
mở form đơn hàng → đọc grid" mà không cần người can thiệp.

### GĐ 3 — Visual (2 ngày)
- [ ] `ScreenshotService` (downscale, giới hạn 4MB, PNG/JPEG).
- [ ] `wf_screenshot` trả `ImageContentBlock` + text mô tả.
- [ ] Verify thủ công ở DPI 100% và 150%.

**DoD:** Claude mô tả đúng nội dung form từ ảnh chụp; mọi ảnh dưới 4MB.

### GĐ 4 — Static analysis (5–6 ngày)
- [ ] `FormAnalyzer` (partial cluster + Compilation + reference WinForms).
- [ ] `DesignerModel` parse `InitializeComponent`.
- [ ] Rules theo thứ tự: `WF040` → `WF001/WF002` → `WF020/WF021` → `WF010/WF011` → `WF030/WF031` → còn lại.
- [ ] `wf_analyze_form`, `wf_analyze_project`, `wf_list_rules`.
- [ ] `BadLayoutForm` + unit test cho từng rule.

**DoD:** `wf_analyze_project` trên `SampleApp` bắt đúng 100% vi phạm đã cài cắm trong `BadLayoutForm`,
và không có false positive trên các form còn lại.

### GĐ 5 — Hardening (2–3 ngày)
- [ ] Review lại toàn bộ `[Description]` — viết cho agent đọc, không phải cho dev.
- [ ] README: ràng buộc môi trường §2, troubleshooting, cảnh báo bảo mật.
- [ ] Chuẩn hoá timeout mặc định từng tool.
- [ ] Chạy end-to-end trên **một app WinForms thật** ngoài SampleApp.

**Tổng: ~4 tuần.** (v1 ước 2 tuần — không thực tế với scope này.)

---

## 15. Sổ rủi ro

| Rủi ro | Mức | Giảm thiểu |
| :--- | :--- | :--- |
| Control WinForms không có `AutomationId` ổn định | **Cao** | Selector đa chiến lược §5.1 + fuzzy candidates + rule `WF040` chỉ ra chỗ cần sửa trong code |
| Flakiness của UI automation | **Cao** | `Retry.WhileNull` ở mọi lần tìm; `wf_wait_for`/`wf_wait_idle`; verify sau thao tác |
| Nổ context do cây UI / ảnh quá lớn | **Cao** | maxNodes/maxDepth + format text compact + downscale ảnh + cảnh báo khi bị cắt |
| Tool treo làm chết MCP session | Trung bình | Timeout cứng ở `UiThread.RunAsync` + poison detection + hướng dẫn recover trong message lỗi |
| Log lọt stdout phá JSON-RPC | Trung bình | `LogToStandardErrorThreshold` + protocol smoke test trong CI |
| Control third-party không tương tác được | Trung bình | Đã tuyên bố out-of-scope §1.3; fallback physical click |
| Process WinForms mồ côi | Thấp | Cleanup ở `ApplicationStopping` + `ProcessExit` |
| Lệch DPI | Thấp | Manifest §9 + test thủ công ở 2 mức scale |

---

## 16. Quyết định mở — cần chốt trước GĐ 1

1. **Đa session?** Hiện thiết kế 1 app tại một thời điểm. Nếu cần test giao tiếp giữa 2 app, phải đổi
   `UiSession` thành `Dictionary<string, UiSession>` với `sessionId`.
   → *Khuyến nghị: giữ đơn session cho v1, nhưng thiết kế API đã sẵn sàng mở rộng.*
2. **Ngôn ngữ output?** ✅ **ĐÃ CHỐT (ngược với khuyến nghị ban đầu):** `code` giữ tiếng Anh
   (`ELEMENT_NOT_FOUND`, ...), còn `message` và `hint` đều tiếng Việt. Lý do: người đọc trực tiếp
   output là người Việt, và thực tế agent xử lý tốt. Quy ước này đã áp dụng toàn bộ codebase — xem `CLAUDE.md`.
3. **Có cần `wf_eval`** (chạy code C# tuỳ ý trong process đích qua injection)? Rất mạnh nhưng rủi ro cao.
   → *Khuyến nghị: KHÔNG — đã đưa vào out-of-scope §1.3.*
