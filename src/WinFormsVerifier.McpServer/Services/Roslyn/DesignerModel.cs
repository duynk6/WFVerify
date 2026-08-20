using System.Drawing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace WinFormsVerifier.Services.Roslyn;

public class DesignerControlNode
{
    public string FieldName { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Text { get; set; }
    public Point? Location { get; set; }
    public Size? Size { get; set; }
    public Size? ClientSize { get; set; }
    public int? TabIndex { get; set; }
    public string? Anchor { get; set; }
    public string? Dock { get; set; }
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public string? AccessibleName { get; set; }
    public string? Font { get; set; }
    public string? AutoScaleMode { get; set; }
    public string? ParentFieldName { get; set; }
    public bool IsAddedToParent { get; set; }
    public bool IsForm { get; set; }
    public int LineNumber { get; set; }
    public string FileName { get; set; } = string.Empty;

    public List<EventWiringInfo> EventWirings { get; set; } = new();
    public List<DesignerControlNode> Children { get; set; } = new();
}

public class EventWiringInfo
{
    public string EventName { get; set; } = string.Empty;
    public string HandlerName { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public class DesignerModel
{
    public string FormClassName { get; set; } = string.Empty;
    public DesignerControlNode RootForm { get; set; } = new();
    public Dictionary<string, DesignerControlNode> ControlsByField { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<EventWiringInfo> AllEventWirings { get; set; } = new();

    public static DesignerModel Parse(SyntaxTree tree, string formClassName)
    {
        var model = new DesignerModel { FormClassName = formClassName };
        var root = tree.GetRoot();
        var filePath = tree.FilePath;

        // Find InitializeComponent method
        var initMethod = root.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == "InitializeComponent");

        if (initMethod == null || initMethod.Body == null)
        {
            return model;
        }

        var formNode = new DesignerControlNode
        {
            FieldName = "this",
            Name = formClassName,
            TypeName = "Form",
            IsForm = true,
            FileName = filePath,
            LineNumber = initMethod.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        };
        model.RootForm = formNode;
        model.ControlsByField["this"] = formNode;

        // 1. Scan control statements in InitializeComponent
        foreach (var statement in initMethod.Body.Statements)
        {
            if (statement is ExpressionStatementSyntax expStmt)
            {
                ParseStatement(expStmt.Expression, model, filePath);
            }
        }

        // Build parent-child relationships only for controls added to parents
        foreach (var kvp in model.ControlsByField)
        {
            var node = kvp.Value;
            if (node.IsForm) continue;

            if (node.IsAddedToParent)
            {
                var parentField = !string.IsNullOrEmpty(node.ParentFieldName) ? node.ParentFieldName : "this";
                if (model.ControlsByField.TryGetValue(parentField, out var parent))
                {
                    parent.Children.Add(node);
                }
                else
                {
                    formNode.Children.Add(node);
                }
            }
        }

        return model;
    }

    private static void ParseStatement(ExpressionSyntax expr, DesignerModel model, string filePath)
    {
        var line = expr.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

        // 1. Event wiring: this.button1.Click += new System.EventHandler(this.button1_Click);
        if (expr is AssignmentExpressionSyntax addAssign && addAssign.IsKind(SyntaxKind.AddAssignmentExpression))
        {
            var leftStr = addAssign.Left.ToString();
            var (targetField, eventName) = ParseMemberAccess(leftStr);
            var handlerName = ExtractHandlerName(addAssign.Right);

            if (!string.IsNullOrEmpty(handlerName) && !string.IsNullOrEmpty(eventName))
            {
                var wiring = new EventWiringInfo
                {
                    EventName = eventName,
                    HandlerName = handlerName,
                    LineNumber = line,
                    FileName = filePath
                };
                model.AllEventWirings.Add(wiring);

                var targetNode = GetOrAddNode(model, targetField, filePath, line);
                targetNode.EventWirings.Add(wiring);
            }
            return;
        }

        // 2. Object instantiation or property assignment: this.button1 = new Button() or this.button1.Text = "Hello"
        if (expr is AssignmentExpressionSyntax assignExpr)
        {
            var leftStr = assignExpr.Left.ToString();
            var (targetField, propName) = ParseMemberAccess(leftStr);

            if (string.IsNullOrEmpty(propName))
            {
                // Control instantiation: this.button1 = new Button()
                var node = GetOrAddNode(model, targetField, filePath, line);
                if (assignExpr.Right is ObjectCreationExpressionSyntax creation)
                {
                    node.TypeName = creation.Type.ToString();
                }
                return;
            }

            var targetNode = GetOrAddNode(model, targetField, filePath, line);
            AssignProperty(targetNode, propName, assignExpr.Right);
            return;
        }

        // 3. Controls.Add / Controls.AddRange
        if (expr is InvocationExpressionSyntax invocation)
        {
            var callStr = invocation.Expression.ToString();
            if (callStr.EndsWith(".Controls.Add", StringComparison.Ordinal) ||
                callStr.EndsWith(".Controls.AddRange", StringComparison.Ordinal))
            {
                var parentField = "this";
                var controlsIdx = callStr.IndexOf(".Controls", StringComparison.Ordinal);
                if (controlsIdx > 0)
                {
                    parentField = NormalizeFieldName(callStr[..controlsIdx]);
                }

                foreach (var arg in invocation.ArgumentList.Arguments)
                {
                    if (arg.Expression is ArrayCreationExpressionSyntax arrayCreation && arrayCreation.Initializer != null)
                    {
                        foreach (var elem in arrayCreation.Initializer.Expressions)
                        {
                            AddChildToParent(model, elem.ToString(), parentField, filePath, line);
                        }
                    }
                    else if (arg.Expression is ImplicitArrayCreationExpressionSyntax implicitArray && implicitArray.Initializer != null)
                    {
                        foreach (var elem in implicitArray.Initializer.Expressions)
                        {
                            AddChildToParent(model, elem.ToString(), parentField, filePath, line);
                        }
                    }
                    else
                    {
                        AddChildToParent(model, arg.Expression.ToString(), parentField, filePath, line);
                    }
                }
            }
        }
    }

    private static void AddChildToParent(DesignerModel model, string childExpr, string parentField, string filePath, int line)
    {
        var childField = NormalizeFieldName(childExpr);
        var childNode = GetOrAddNode(model, childField, filePath, line);
        childNode.ParentFieldName = parentField;
        childNode.IsAddedToParent = true;
    }

    private static (string TargetField, string? PropertyName) ParseMemberAccess(string exprStr)
    {
        var clean = exprStr.Trim();
        if (clean.StartsWith("this.", StringComparison.Ordinal))
        {
            clean = clean[5..];
        }

        var parts = clean.Split('.');
        if (parts.Length == 1)
        {
            if (IsFormProperty(parts[0]))
            {
                return ("this", parts[0]);
            }
            return ("this." + parts[0], null);
        }
        else if (parts.Length == 2)
        {
            return ("this." + parts[0], parts[1]);
        }
        else
        {
            var field = "this." + string.Join('.', parts[..^1]);
            return (field, parts[^1]);
        }
    }

    private static bool IsFormProperty(string name)
    {
        return name is "ClientSize" or "Size" or "Location" or "Text" or "Name" or "Font" or
                       "AutoScaleMode" or "AutoScaleDimensions" or "StartPosition" or "FormBorderStyle" or
                       "MaximizeBox" or "MinimizeBox" or "MainMenuStrip" or "AutoScroll" or "Padding";
    }

    private static string NormalizeFieldName(string field)
    {
        var clean = field.Trim();
        if (clean.StartsWith("this.", StringComparison.Ordinal)) return clean;
        if (clean == "this") return "this";
        return "this." + clean;
    }

    private static DesignerControlNode GetOrAddNode(DesignerModel model, string fieldName, string filePath, int line)
    {
        var norm = NormalizeFieldName(fieldName);
        if (!model.ControlsByField.TryGetValue(norm, out var node))
        {
            node = new DesignerControlNode
            {
                FieldName = norm,
                Name = norm.Replace("this.", "", StringComparison.Ordinal),
                FileName = filePath,
                LineNumber = line
            };
            model.ControlsByField[norm] = node;
        }
        return node;
    }

    private static void AssignProperty(DesignerControlNode node, string propName, ExpressionSyntax right)
    {
        var rightText = right.ToString().Trim('\"', ' ');

        switch (propName)
        {
            case "Name":
                node.Name = rightText;
                break;
            case "Text":
                node.Text = rightText;
                break;
            case "AccessibleName":
                node.AccessibleName = rightText;
                break;
            case "TabIndex":
                if (int.TryParse(rightText, out var ti)) node.TabIndex = ti;
                break;
            case "Visible":
                if (bool.TryParse(rightText, out var vis)) node.Visible = vis;
                break;
            case "Enabled":
                if (bool.TryParse(rightText, out var en)) node.Enabled = en;
                break;
            case "Anchor":
                node.Anchor = rightText;
                break;
            case "Dock":
                node.Dock = rightText;
                break;
            case "Font":
                node.Font = rightText;
                break;
            case "AutoScaleMode":
                node.AutoScaleMode = rightText;
                break;
            case "Location":
                node.Location = ParsePoint(right);
                break;
            case "Size":
                node.Size = ParseSize(right);
                break;
            case "ClientSize":
                node.ClientSize = ParseSize(right);
                break;
        }
    }

    private static Point? ParsePoint(ExpressionSyntax expr)
    {
        if (expr is ObjectCreationExpressionSyntax oc && oc.ArgumentList?.Arguments.Count == 2)
        {
            var xStr = oc.ArgumentList.Arguments[0].Expression.ToString();
            var yStr = oc.ArgumentList.Arguments[1].Expression.ToString();

            if (int.TryParse(xStr, out var x) && int.TryParse(yStr, out var y))
            {
                return new Point(x, y);
            }
        }
        return null;
    }

    private static Size? ParseSize(ExpressionSyntax expr)
    {
        if (expr is ObjectCreationExpressionSyntax oc && oc.ArgumentList?.Arguments.Count == 2)
        {
            var wStr = oc.ArgumentList.Arguments[0].Expression.ToString();
            var hStr = oc.ArgumentList.Arguments[1].Expression.ToString();

            if (int.TryParse(wStr, out var w) && int.TryParse(hStr, out var h))
            {
                return new Size(w, h);
            }
        }
        return null;
    }

    private static string ExtractHandlerName(ExpressionSyntax expr)
    {
        if (expr is ObjectCreationExpressionSyntax oc && oc.ArgumentList?.Arguments.Count > 0)
        {
            return CleanMethodName(oc.ArgumentList.Arguments[0].Expression.ToString());
        }
        return CleanMethodName(expr.ToString());
    }

    private static string CleanMethodName(string raw)
    {
        return raw.Replace("this.", "", StringComparison.Ordinal)
                  .Replace("new EventHandler(", "", StringComparison.Ordinal)
                  .Replace(")", "", StringComparison.Ordinal)
                  .Trim();
    }
}
