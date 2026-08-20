# WinForms Verifier MCP Server — Technical Implementation Guide

> **Mục tiêu:** Xây dựng máy chủ MCP (Model Context Protocol) chuyên dụng cho Windows Forms (.NET C#) giúp Claude Code (hoặc Claude Desktop, Cursor) có khả năng thanh tra giao diện (UI Inspection), tự động tương tác, kiểm thử runtime (UI Automation) và phân tích tĩnh file mã nguồn (`.Designer.cs`).

---

## 1. Tổng quan Kiến trúc & Nguyên lý hoạt động

Mô hình kết hợp giữa **Claude Code** và **WinForms Verifier MCP Server**:

```
+-------------------------------------------------------------+
|                     Claude Code (AI Agent)                  |
+-------------------------------------------------------------+
                              | (JSON-RPC via stdio)
                              v
+-------------------------------------------------------------+
|              WinForms Verifier MCP Server (.NET 8/9)        |
|  +-------------------------+  +--------------------------+  |
|  | Roslyn Code Engine      |  | FlaUI / UIA3 Engine      |  |
|  | (Static & Design Check) |  | (Runtime & UI Inspection)|  |
|  +-------------------------+  +--------------------------+  |
+-------------------------------------------------------------+
              |                                 |
              v                                 v
   [ .Designer.cs / Code ]              [ Running WinForms App ]
```

- **Static Analysis (Roslyn):** Quét cú pháp cây AST của C# để kiểm tra thuộc tính Control, sự kiện mồ côi (orphaned event handlers), TabIndex, layout docking mà không cần chạy ứng dụng.
- **Dynamic UI Automation (FlaUI.UIA3):** Gắn kết trực tiếp vào tiến trình WinForms đang chạy hoặc khởi chạy mới, duyệt cây đối tượng giao diện (AutomationElements), gửi phím/chuột và chụp ảnh màn hình Form.

---

## 2. Cấu trúc Thư mục Dự án

```
WinFormsVerifier.McpServer/
├── Program.cs                      # Khởi tạo MCP Server & cấu hình stdio listener
├── WinFormsVerifier.csproj          # Cấu hình SDK và dependencies (.NET 8/9 Windows)
├── Tools/
│   ├── StaticAnalysisTools.cs      # Phân tích file .Designer.cs bằng Roslyn
│   ├── AppLifecycleTools.cs        # Khởi chạy, attach, dừng ứng dụng
│   ├── UiInspectionTools.cs        # Lấy cây giao diện (UI Tree), tìm Control theo AutomationId
│   ├── UiInteractionTools.cs       # Thao tác Click, SetText, KeyPress, DataGridView
│   └── VisualVerificationTools.cs  # Chụp ảnh Form/Control trả về Base64 PNG
└── Models/
    ├── ElementInfoDto.cs           # DTO trả về thông tin Control (Id, Name, Bounds, State)
    └── ActionRequestDto.cs         # Schema payload các lệnh tương tác
```

---

## 3. Danh mục MCP Tools Chi Tiết

| Tool Name | Parameters | Kiểu trả về | Mô tả Chức năng |
| :--- | :--- | :--- | :--- |
| `launch_or_attach_app` | `exePath` (string?), `processName` (string?), `arguments` (string?) | `string` | Khởi chạy file `.exe` hoặc kết nối (attach) vào tiến trình WinForms đang chạy. |
| `get_ui_hierarchy` | `windowTitle` (string?), `maxDepth` (int = 10) | `JSON String` | Quét toàn bộ Visual Tree của Form (AutomationId, Name, ClassName, Bounds, Enabled, Value). |
| `interact_element` | `automationId` (string), `actionType` (click \| set_text \| check \| select), `value` (string?) | `string` | Tương tác trực tiếp lên Control (TextBox, Button, CheckBox, ComboBox, DataGridView). |
| `capture_ui_screenshot` | `automationId` (string?), `fullWindow` (bool = true) | `Base64 String` | Chụp ảnh màn hình cửa sổ hoặc control chỉ định để Claude Vision thẩm định giao diện/layout. |
| `verify_designer_code` | `designerFilePath` (string) | `JSON String` | Dùng Roslyn quét file `.Designer.cs` tìm event handler mồ côi, sai TabIndex, Anchor/Docking lỗi. |
| `close_app` | `killProcess` (bool = false) | `string` | Đóng cửa sổ ứng dụng hoặc force-kill process sau khi hoàn thành phiên kiểm thử. |

---

## 4. Hướng dẫn Cài đặt & Mã nguồn Mẫu

### 4.1. File cấu hình dự án (`WinFormsVerifier.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ModelContextProtocol" Version="1.0.0" />
    <PackageReference Include="FlaUI.UIA3" Version="4.0.0" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" />
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
  </ItemGroup>
</Project>
```

### 4.2. File triển khai MCP Server (`Program.cs`)

```csharp
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Microsoft.AspNetCore.Builder;
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Singleton runtime state
Application? runningApp = null;
UIA3Automation? automation = new UIA3Automation();

// 1. Tool: Khởi chạy hoặc Attach vào WinForms App
app.MapMcpTool("launch_or_attach_app", (string? exePath, string? processName) => {
    if (!string.IsNullOrEmpty(exePath)) {
        runningApp = Application.Launch(exePath);
    } else if (!string.IsNullOrEmpty(processName)) {
        runningApp = Application.Attach(processName);
    } else {
        return "Lỗi: Cần cung cấp exePath hoặc processName";
    }
    return $"Đã gắn kết thành công vào ứng dụng (PID: {runningApp.ProcessId})";
});

// 2. Tool: Lấy cây phân cấp Controls (UI Tree)
app.MapMcpTool("get_ui_hierarchy", (string? windowTitle) => {
    if (runningApp == null || automation == null) 
        return "Lỗi: Ứng dụng chưa được khởi chạy hoặc chưa attach.";

    var window = runningApp.GetMainWindow(automation);
    var root = new {
        Title = window.Title,
        ProcessId = runningApp.ProcessId,
        Controls = window.FindAllDescendants().Select(e => new {
            Id = e.AutomationId,
            Name = e.Name,
            Type = e.ControlType.ToString(),
            IsEnabled = e.IsEnabled,
            Rect = new {
                X = e.BoundingRectangle.X,
                Y = e.BoundingRectangle.Y,
                Width = e.BoundingRectangle.Width,
                Height = e.BoundingRectangle.Height
            }
        })
    };
    return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
});

// 3. Tool: Tương tác với Control
app.MapMcpTool("interact_element", (string automationId, string actionType, string? value) => {
    if (runningApp == null || automation == null) 
        return "Lỗi: Ứng dụng chưa được khởi chạy.";

    var window = runningApp.GetMainWindow(automation);
    var element = window.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    if (element == null) 
        return $"Không tìm thấy control với AutomationId: {automationId}";

    switch (actionType.ToLowerInvariant()) {
        case "click":
            element.AsButton().Click();
            break;
        case "set_text":
            element.AsTextBox().Text = value ?? "";
            break;
        default:
            return $"Action '{actionType}' chưa được hỗ trợ.";
    }
    return $"Thực thi thành công '{actionType}' trên control '{automationId}'";
});

// 4. Tool: Chụp ảnh màn hình trực quan (Visual Assertion)
app.MapMcpTool("capture_ui_screenshot", (string? automationId) => {
    if (runningApp == null || automation == null) 
        return "Lỗi: Ứng dụng chưa được khởi chạy.";

    var window = runningApp.GetMainWindow(automation);
    using var image = string.IsNullOrEmpty(automationId)
        ? window.Capture()
        : window.FindFirstDescendant(cf => cf.ByAutomationId(automationId))?.Capture();

    if (image == null) 
        return "Lỗi: Không thể chụp ảnh đối tượng chỉ định.";

    using var ms = new MemoryStream();
    image.Save(ms, ImageFormat.Png);
    return Convert.ToBase64String(ms.ToArray());
});

// 5. Tool: Đóng ứng dụng
app.MapMcpTool("close_app", (bool killProcess) => {
    if (runningApp == null) return "Không có ứng dụng nào đang chạy.";
    if (killProcess) {
        runningApp.Kill();
    } else {
        runningApp.Close();
    }
    runningApp = null;
    return "Đã đóng ứng dụng thành công.";
});

await app.RunMcpServerAsync();
```

---

## 5. Hướng dẫn Cấu hình với Claude Code

Thêm cấu hình máy chủ MCP vào tệp cấu hình của Claude Code (`~/.claude.json` hoặc `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "winforms-verifier": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/path/to/WinFormsVerifier.McpServer/WinFormsVerifier.McpServer.csproj"
      ]
    }
  }
}
```

---

## 6. Kịch bản Mẫu Tương tác với Claude Code

Sau khi cấu hình xong, bạn có thể ra lệnh trực tiếp cho Claude Code qua giao diện dòng lệnh:

```bash
# Kịch bản 1: Mở app và kiểm tra các thành phần giao diện
> "Khởi chạy file bin/Debug/net8.0-windows/SalesApp.exe, lấy danh sách tất cả các TextBox và Button hiện có trên màn hình chính."

# Kịch bản 2: Thực hiện luồng đăng nhập tự động
> "Nhập username 'NV001' vào txtUsername, mật khẩu 'Admin@123' vào txtPassword, sau đó click nút btnLogin."

# Kịch bản 3: Thẩm định trực quan (Visual Assertion)
> "Chụp ảnh màn hình cửa sổ hiện tại và kiểm tra xem bảng DataGridView dgOrders đã tải danh sách đơn hàng thành công chưa."

# Kịch bản 4: Phân tích tĩnh code WinForms
> "Quét tệp MainForm.Designer.cs để kiểm tra xem có control nào bị gán sai sự kiện hoặc thiếu AccessibleName không."
```

---

## 7. Lộ trình Triển khai Dự án (Roadmap)

- **Giai đoạn 1: Core Automation (Tuần 1)**
  - Xây dựng Console Server .NET 8 chuẩn MCP.
  - Tích hợp FlaUI.UIA3 để gắn kết WinForms Process.
  - Cung cấp các công cụ cơ bản: Launch, Attach, Get UI Hierarchy, Click, SetText.
- **Giai đoạn 2: Advanced Verifier & Visual Testing (Tuần 2)**
  - Mở rộng hỗ trợ tương tác nâng cao: `DataGridView`, `ComboBox`, `TabControl`, `TreeView`.
  - Tích hợp Roslyn CSharp Syntax Analyzer cho `.Designer.cs`.
  - Hỗ trợ chụp Base64 PNG kết hợp cùng Claude Vision Assertion.
