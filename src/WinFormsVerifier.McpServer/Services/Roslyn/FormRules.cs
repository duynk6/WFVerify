using System.Drawing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Services.Roslyn;

public static class FormRules
{
    public static readonly List<RuleInfo> AllRules = new()
    {
        new RuleInfo
        {
            Id = "WF001",
            Severity = "error",
            Category = "Event Handling",
            Description = "Event handler được gán (+=) nhưng method không tồn tại trong bất kỳ partial file nào của Form.",
            FixGuidance = "Khai báo method tương ứng trong code-behind (Form.cs) hoặc xóa liên kết sự kiện."
        },
        new RuleInfo
        {
            Id = "WF002",
            Severity = "warning",
            Category = "Event Handling",
            Description = "Method có chữ ký event handler (object sender, EventArgs e) nhưng không được gắn vào bất kỳ sự kiện nào (Handler mồ côi).",
            FixGuidance = "Gắn handler vào sự kiện trong InitializeComponent hoặc xóa method nếu không còn dùng."
        },
        new RuleInfo
        {
            Id = "WF010",
            Severity = "warning",
            Category = "Layout",
            Description = "Hai control cùng cấp (sibling) trong cùng một container bị chồng lấn hình chữ nhật lên nhau.",
            FixGuidance = "Điều chỉnh Location hoặc Size của một trong hai control để tránh che khuất."
        },
        new RuleInfo
        {
            Id = "WF011",
            Severity = "warning",
            Category = "Layout",
            Description = "Control nằm vượt ra ngoài kích thước ClientSize của container cha (bị cắt xén).",
            FixGuidance = "Điều chỉnh lại tọa độ/kích thước control hoặc mở rộng ClientSize của container."
        },
        new RuleInfo
        {
            Id = "WF012",
            Severity = "info",
            Category = "Layout",
            Description = "Control có tọa độ Location âm (X < 0 hoặc Y < 0).",
            FixGuidance = "Đặt lại Location về giá trị dương phù hợp."
        },
        new RuleInfo
        {
            Id = "WF020",
            Severity = "warning",
            Category = "Accessibility & Navigation",
            Description = "Trùng TabIndex giữa các control tương tác cùng cấp trong cùng container.",
            FixGuidance = "Đánh số lại TabIndex theo thứ tự tăng dần duy nhất."
        },
        new RuleInfo
        {
            Id = "WF021",
            Severity = "info",
            Category = "Accessibility & Navigation",
            Description = "TabIndex không liên tục hoặc control tương tác thiếu TabIndex.",
            FixGuidance = "Gán TabIndex liên tục từ 0 trở đi cho tất cả control tương tác."
        },
        new RuleInfo
        {
            Id = "WF022",
            Severity = "info",
            Category = "Accessibility & Navigation",
            Description = "Thứ tự TabIndex không khớp với thứ tự đọc trực quan từ trên xuống dưới, từ trái qua phải.",
            FixGuidance = "Sắp xếp lại TabIndex theo thứ tự quét màn hình trực quan."
        },
        new RuleInfo
        {
            Id = "WF030",
            Severity = "error",
            Category = "Layout Constraints",
            Description = "Control được đặt Dock = Fill kết hợp với Anchor khác mặc định (Anchor bị vô hiệu hóa ngầm).",
            FixGuidance = "Chọn hoặc Dock = Fill hoặc cấu hình Anchor, không dùng cả hai cùng lúc."
        },
        new RuleInfo
        {
            Id = "WF031",
            Severity = "warning",
            Category = "Layout Constraints",
            Description = "Control trong Form có thể resize nhưng giữ Anchor mặc định (Top, Left) nên không tự co giãn theo cửa sổ.",
            FixGuidance = "Cân nhắc đặt Anchor gồm Right hoặc Bottom nếu muốn control co giãn khi phóng to cửa sổ."
        },
        new RuleInfo
        {
            Id = "WF040",
            Severity = "warning",
            Category = "AI Automation & Accessibility",
            Description = "Control tương tác (Button, TextBox, ComboBox, DataGridView) thiếu AccessibleName và Text rỗng hoặc để tên mặc định — MCP Server không thể định vị ổn định.",
            FixGuidance = "Đặt thuộc tính AccessibleName hoặc Name có ý nghĩa nghiệp vụ rõ ràng."
        },
        new RuleInfo
        {
            Id = "WF041",
            Severity = "info",
            Category = "Naming Conventions",
            Description = "Control vẫn giữ tên mặc định do Visual Studio sinh ra (vd: button1, textBox2, label3).",
            FixGuidance = "Đổi tên control theo tiền tố chuẩn (vd: btnLuu, txtMaKH, lblTieuDe)."
        },
        new RuleInfo
        {
            Id = "WF050",
            Severity = "warning",
            Category = "Theming & DPI",
            Description = "Font chữ bị hardcode riêng cho control thay vì kế thừa font mặc định của Form, gây nguy cơ lệch layout khi đổi DPI.",
            FixGuidance = "Bỏ hardcode Font trên control con hoặc sử dụng Form.Font thống nhất."
        },
        new RuleInfo
        {
            Id = "WF051",
            Severity = "info",
            Category = "Theming & DPI",
            Description = "Form chưa cấu hình AutoScaleMode hoặc đặt AutoScaleMode = None (rủi ro vỡ layout trên màn hình DPI cao).",
            FixGuidance = "Đặt AutoScaleMode = AutoScaleMode.Dpi hoặc AutoScaleMode.Font."
        },
        new RuleInfo
        {
            Id = "WF060",
            Severity = "info",
            Category = "Dead Code",
            Description = "Control được khai báo và khởi tạo nhưng không được thêm vào Controls.Add của bất kỳ container nào.",
            FixGuidance = "Thêm control vào container cha hoặc xóa khai báo nếu không dùng."
        }
    };

    public static List<DiagnosticItem> RunRules(
        DesignerModel model,
        HashSet<string> declaredMethods,
        List<(string Name, int Line, string File)> candidateHandlerMethods,
        HashSet<string>? enabledRuleIds = null)
    {
        var diagnostics = new List<DiagnosticItem>();

        bool ShouldRun(string ruleId) => enabledRuleIds == null || enabledRuleIds.Contains(ruleId);

        // WF001: Wired handler not declared
        if (ShouldRun("WF001"))
        {
            foreach (var wiring in model.AllEventWirings)
            {
                if (!declaredMethods.Contains(wiring.HandlerName))
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Rule = "WF001",
                        Severity = "error",
                        File = Path.GetFileName(wiring.FileName),
                        Line = wiring.LineNumber,
                        Message = $"Sự kiện '{wiring.EventName}' được gán handler '{wiring.HandlerName}' nhưng method này không tồn tại trong code-behind.",
                        Fix = $"Tạo method 'private void {wiring.HandlerName}(object sender, EventArgs e)' trong Form.cs hoặc xóa dòng gán sự kiện."
                    });
                }
            }
        }

        // WF002: Orphaned handler methods
        if (ShouldRun("WF002"))
        {
            var wiredHandlers = new HashSet<string>(model.AllEventWirings.Select(w => w.HandlerName), StringComparer.OrdinalIgnoreCase);
            foreach (var (methodName, line, file) in candidateHandlerMethods)
            {
                if (!wiredHandlers.Contains(methodName))
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Rule = "WF002",
                        Severity = "warning",
                        File = Path.GetFileName(file),
                        Line = line,
                        Message = $"Method '{methodName}' có chữ ký event handler nhưng không được gắn vào sự kiện nào trong Designer.",
                        Fix = $"Gắn '{methodName}' vào sự kiện tương ứng trong InitializeComponent hoặc xóa nếu không còn sử dụng."
                    });
                }
            }
        }

        // WF051: AutoScaleMode
        if (ShouldRun("WF051"))
        {
            var mode = model.RootForm.AutoScaleMode;
            if (string.IsNullOrEmpty(mode) || mode.Contains("None", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticItem
                {
                    Rule = "WF051",
                    Severity = "info",
                    Control = model.RootForm.Name,
                    File = Path.GetFileName(model.RootForm.FileName),
                    Line = model.RootForm.LineNumber,
                    Message = $"Form '{model.RootForm.Name}' chưa cấu hình AutoScaleMode hoặc đang đặt là None.",
                    Fix = "Đặt this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi; hoặc AutoScaleMode.Font;"
                });
            }
        }

        // Check each container for layout & tab rules
        CheckContainerHierarchy(model.RootForm, model, diagnostics, ShouldRun);

        // Check all individual controls
        foreach (var kvp in model.ControlsByField)
        {
            var node = kvp.Value;
            if (node.IsForm) continue;

            // WF012: Negative location
            if (ShouldRun("WF012") && node.Location.HasValue)
            {
                if (node.Location.Value.X < 0 || node.Location.Value.Y < 0)
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Rule = "WF012",
                        Severity = "info",
                        Control = node.Name,
                        File = Path.GetFileName(node.FileName),
                        Line = node.LineNumber,
                        Message = $"Control '{node.Name}' có tọa độ âm ({node.Location.Value.X}, {node.Location.Value.Y}).",
                        Fix = "Chỉnh sửa Location để X và Y >= 0."
                    });
                }
            }

            // WF030: Dock Fill + Anchor
            if (ShouldRun("WF030"))
            {
                if (node.Dock != null && node.Dock.Contains("Fill", StringComparison.OrdinalIgnoreCase) &&
                    node.Anchor != null && !node.Anchor.Contains("Top", StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Rule = "WF030",
                        Severity = "error",
                        Control = node.Name,
                        File = Path.GetFileName(node.FileName),
                        Line = node.LineNumber,
                        Message = $"Control '{node.Name}' đặt Dock = Fill nhưng đồng thời chỉ định Anchor '{node.Anchor}'.",
                        Fix = "Xóa thuộc tính Anchor hoặc đổi Dock = None."
                    });
                }
            }

            // WF040: Interactive control missing AccessibleName & Text
            if (ShouldRun("WF040") && IsInteractiveControl(node.TypeName))
            {
                bool hasAccessName = !string.IsNullOrWhiteSpace(node.AccessibleName);
                bool hasText = !string.IsNullOrWhiteSpace(node.Text);
                bool hasDefaultName = IsDefaultControlName(node.Name);

                if (!hasAccessName && (!hasText || hasDefaultName))
                {
                    diagnostics.Add(new DiagnosticItem
                    {
                        Rule = "WF040",
                        Severity = "warning",
                        Control = node.Name,
                        File = Path.GetFileName(node.FileName),
                        Line = node.LineNumber,
                        Message = $"Control tương tác '{node.Name}' ({node.TypeName}) thiếu AccessibleName và có tên mặc định — gây khó khăn cho AI UI automation.",
                        Fix = $"Đặt AccessibleName có ý nghĩa hoặc đổi tên control '{node.Name}' thành tên nghiệp vụ."
                    });
                }
            }

            // WF041: Default control name
            if (ShouldRun("WF041") && IsDefaultControlName(node.Name))
            {
                diagnostics.Add(new DiagnosticItem
                {
                    Rule = "WF041",
                    Severity = "info",
                    Control = node.Name,
                    File = Path.GetFileName(node.FileName),
                    Line = node.LineNumber,
                    Message = $"Control '{node.Name}' vẫn giữ tên mặc định của Visual Studio.",
                    Fix = $"Đổi tên '{node.Name}' theo quy ước đặt tên (vd: txtTen, btnLuu, cboLoai)."
                });
            }

            // WF050: Hardcoded font
            if (ShouldRun("WF050") && !string.IsNullOrEmpty(node.Font))
            {
                diagnostics.Add(new DiagnosticItem
                {
                    Rule = "WF050",
                    Severity = "warning",
                    Control = node.Name,
                    File = Path.GetFileName(node.FileName),
                    Line = node.LineNumber,
                    Message = $"Control '{node.Name}' có font chữ hardcode ({node.Font}) khác với font chung của Form.",
                    Fix = "Xóa gán Font trên control con để tự động kế thừa font từ Form cha."
                });
            }

            // WF060: Not added to parent
            if (ShouldRun("WF060") && !node.IsAddedToParent)
            {
                diagnostics.Add(new DiagnosticItem
                {
                    Rule = "WF060",
                    Severity = "info",
                    Control = node.Name,
                    File = Path.GetFileName(node.FileName),
                    Line = node.LineNumber,
                    Message = $"Control '{node.Name}' ({node.TypeName}) được khởi tạo nhưng không được thêm vào Controls.Add của bất kỳ container nào.",
                    Fix = $"Thêm 'this.Controls.Add(this.{node.Name});' hoặc xóa control nếu không dùng."
                });
            }
        }

        return diagnostics;
    }

    private static void CheckContainerHierarchy(
        DesignerControlNode container,
        DesignerModel model,
        List<DiagnosticItem> diagnostics,
        Func<string, bool> shouldRun)
    {
        var children = container.Children.Where(c => c.Visible).ToList();

        // WF010: Overlap check
        if (shouldRun("WF010"))
        {
            for (int i = 0; i < children.Count; i++)
            {
                for (int j = i + 1; j < children.Count; j++)
                {
                    var a = children[i];
                    var b = children[j];

                    if (a.Location.HasValue && a.Size.HasValue && b.Location.HasValue && b.Size.HasValue)
                    {
                        var rectA = new Rectangle(a.Location.Value, a.Size.Value);
                        var rectB = new Rectangle(b.Location.Value, b.Size.Value);

                        if (rectA.IntersectsWith(rectB))
                        {
                            diagnostics.Add(new DiagnosticItem
                            {
                                Rule = "WF010",
                                Severity = "warning",
                                Control = $"{a.Name} & {b.Name}",
                                File = Path.GetFileName(a.FileName),
                                Line = a.LineNumber,
                                Message = $"Hai control '{a.Name}' và '{b.Name}' trong cùng container '{container.Name}' bị chồng lấn tọa độ.",
                                Fix = "Điều chỉnh Location hoặc Size của một trong hai control để không đè lên nhau."
                            });
                        }
                    }
                }
            }
        }

        // WF011: Bounds outside parent ClientSize
        if (shouldRun("WF011") && container.ClientSize.HasValue)
        {
            var pW = container.ClientSize.Value.Width;
            var pH = container.ClientSize.Value.Height;

            foreach (var child in children)
            {
                if (child.Location.HasValue && child.Size.HasValue)
                {
                    var right = child.Location.Value.X + child.Size.Value.Width;
                    var bottom = child.Location.Value.Y + child.Size.Value.Height;

                    if (right > pW || bottom > pH)
                    {
                        diagnostics.Add(new DiagnosticItem
                        {
                            Rule = "WF011",
                            Severity = "warning",
                            Control = child.Name,
                            File = Path.GetFileName(child.FileName),
                            Line = child.LineNumber,
                            Message = $"Control '{child.Name}' (kích thước {right}x{bottom}) nằm vượt quá ClientSize của '{container.Name}' ({pW}x{pH}).",
                            Fix = "Thu nhỏ control hoặc tăng ClientSize của container cha."
                        });
                    }
                }
            }
        }

        // WF020: Duplicate TabIndex
        if (shouldRun("WF020"))
        {
            var tabGroups = children
                .Where(c => c.TabIndex.HasValue && IsInteractiveControl(c.TypeName))
                .GroupBy(c => c.TabIndex!.Value)
                .Where(g => g.Count() > 1);

            foreach (var group in tabGroups)
            {
                var names = string.Join(", ", group.Select(g => g.Name));
                var first = group.First();
                diagnostics.Add(new DiagnosticItem
                {
                    Rule = "WF020",
                    Severity = "warning",
                    Control = names,
                    File = Path.GetFileName(first.FileName),
                    Line = first.LineNumber,
                    Message = $"Trùng TabIndex={group.Key} giữa các control: [{names}].",
                    Fix = "Đánh số lại TabIndex riêng biệt cho từng control."
                });
            }
        }

        // Recurse for nested containers
        foreach (var child in container.Children)
        {
            if (child.Children.Count > 0)
            {
                CheckContainerHierarchy(child, model, diagnostics, shouldRun);
            }
        }
    }

    private static bool IsInteractiveControl(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName)) return false;
        var clean = typeName.Replace("System.Windows.Forms.", "");
        return clean is "Button" or "TextBox" or "MaskedTextBox" or "ComboBox" or "CheckBox" or "RadioButton" or
                        "DataGridView" or "ListBox" or "CheckedListBox" or "DateTimePicker" or "NumericUpDown" or
                        "TreeView" or "ListView" or "LinkLabel";
    }

    private static bool IsDefaultControlName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return true;
        var lower = name.ToLowerInvariant();
        return (lower.StartsWith("button", StringComparison.Ordinal) ||
                lower.StartsWith("textbox", StringComparison.Ordinal) ||
                lower.StartsWith("label", StringComparison.Ordinal) ||
                lower.StartsWith("combobox", StringComparison.Ordinal) ||
                lower.StartsWith("checkbox", StringComparison.Ordinal) ||
                lower.StartsWith("panel", StringComparison.Ordinal) ||
                lower.StartsWith("datagridview", StringComparison.Ordinal) ||
                lower.StartsWith("radiobutton", StringComparison.Ordinal)) &&
               char.IsDigit(name[^1]);
    }
}
