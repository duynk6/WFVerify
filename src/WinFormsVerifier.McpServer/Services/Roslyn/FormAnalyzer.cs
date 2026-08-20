using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WinFormsVerifier.Infrastructure;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Services.Roslyn;

public sealed class FormAnalyzer
{
    public FormAnalysisResult AnalyzeForm(
        string formFilePath,
        string? rules = null,
        string minSeverity = "info")
    {
        var fullPath = PathGuard.ValidateAndNormalize(formFilePath, nameof(formFilePath));
        if (!File.Exists(fullPath))
        {
            throw new ToolException(ErrorCode.PathDenied, $"Không tìm thấy file mã nguồn tại '{fullPath}'.");
        }

        var dir = Path.GetDirectoryName(fullPath)!;
        var fileName = Path.GetFileName(fullPath);
        var baseName = fileName.Replace(".Designer.cs", "").Replace(".cs", "");

        // Find all partial files for this form
        var candidateFiles = Directory.GetFiles(dir, $"{baseName}*.cs")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return name.Equals($"{baseName}.cs", StringComparison.OrdinalIgnoreCase) ||
                       name.Equals($"{baseName}.Designer.cs", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith($"{baseName}.", StringComparison.OrdinalIgnoreCase);
            })
            .ToList();

        if (candidateFiles.Count == 0)
        {
            candidateFiles.Add(fullPath);
        }

        var syntaxTrees = new List<SyntaxTree>();
        var declaredMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidateHandlers = new List<(string Name, int Line, string File)>();
        SyntaxTree? designerTree = null;

        foreach (var file in candidateFiles)
        {
            var code = File.ReadAllText(file);
            var tree = CSharpSyntaxTree.ParseText(code, path: file);
            syntaxTrees.Add(tree);

            var root = tree.GetRoot();

            // Collect methods
            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var m in methods)
            {
                var methodName = m.Identifier.Text;
                declaredMethods.Add(methodName);

                // Check if this method looks like an event handler: (object, EventArgs) or named *_Click / *_Load
                if (IsPotentialEventHandler(m))
                {
                    var line = m.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    candidateHandlers.Add((methodName, line, file));
                }

                if (methodName == "InitializeComponent")
                {
                    designerTree = tree;
                }
            }
        }

        if (designerTree == null)
        {
            // If no InitializeComponent, try parsing the input file anyway
            designerTree = syntaxTrees[0];
        }

        var designerModel = DesignerModel.Parse(designerTree, baseName);

        HashSet<string>? enabledRuleSet = null;
        if (!string.IsNullOrWhiteSpace(rules))
        {
            enabledRuleSet = new HashSet<string>(
                rules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        var allDiagnostics = FormRules.RunRules(designerModel, declaredMethods, candidateHandlers, enabledRuleSet);

        // Filter by minSeverity
        var filteredDiagnostics = allDiagnostics
            .Where(d => SeverityMeetsThreshold(d.Severity, minSeverity))
            .OrderByDescending(d => GetSeverityWeight(d.Severity))
            .ThenBy(d => d.Line)
            .ToList();

        var summary = new Dictionary<string, int>
        {
            ["error"] = filteredDiagnostics.Count(d => d.Severity == "error"),
            ["warning"] = filteredDiagnostics.Count(d => d.Severity == "warning"),
            ["info"] = filteredDiagnostics.Count(d => d.Severity == "info")
        };

        return new FormAnalysisResult
        {
            Form = baseName,
            Files = candidateFiles.Select(Path.GetFileName).ToList()!,
            ControlCount = designerModel.ControlsByField.Count(c => !c.Value.IsForm),
            Diagnostics = filteredDiagnostics,
            Summary = summary
        };
    }

    public ProjectAnalysisResult AnalyzeProject(
        string projectPath,
        string minSeverity = "warning",
        int maxForms = 50)
    {
        var fullProjPath = PathGuard.ValidateAndNormalize(projectPath, nameof(projectPath));
        if (!File.Exists(fullProjPath))
        {
            throw new ToolException(ErrorCode.PathDenied, $"Không tìm thấy file project tại '{fullProjPath}'.");
        }

        var projDir = Path.GetDirectoryName(fullProjPath)!;

        // Find all *.Designer.cs files in project
        var designerFiles = Directory.GetFiles(projDir, "*.Designer.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\Properties\\"))
            .Take(maxForms)
            .ToList();

        var formResults = new List<FormAnalysisResult>();
        int totalErrors = 0;
        int totalWarnings = 0;
        int totalInfos = 0;

        foreach (var designerFile in designerFiles)
        {
            try
            {
                var result = AnalyzeForm(designerFile, minSeverity: minSeverity);
                formResults.Add(result);

                totalErrors += result.Summary.GetValueOrDefault("error", 0);
                totalWarnings += result.Summary.GetValueOrDefault("warning", 0);
                totalInfos += result.Summary.GetValueOrDefault("info", 0);
            }
            catch
            {
                // Continue analyzing other forms
            }
        }

        return new ProjectAnalysisResult
        {
            Project = Path.GetFileName(fullProjPath),
            FormsAnalyzed = formResults.Count,
            Forms = formResults,
            Summary = new Dictionary<string, int>
            {
                ["error"] = totalErrors,
                ["warning"] = totalWarnings,
                ["info"] = totalInfos
            }
        };
    }

    private static bool IsPotentialEventHandler(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.Text;
        if (name == "InitializeComponent" || name == "Dispose") return false;

        // Check naming pattern
        if (name.Contains('_') ||
            name.EndsWith("Click", StringComparison.Ordinal) ||
            name.EndsWith("Load", StringComparison.Ordinal) ||
            name.EndsWith("Changed", StringComparison.Ordinal))
        {
            return true;
        }

        // Check parameter signature: (object sender, EventArgs e)
        var parameters = method.ParameterList.Parameters;
        if (parameters.Count == 2)
        {
            var p2Type = parameters[1].Type?.ToString() ?? "";
            if (p2Type.Contains("EventArgs", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SeverityMeetsThreshold(string severity, string minSeverity)
    {
        return GetSeverityWeight(severity) >= GetSeverityWeight(minSeverity);
    }

    private static int GetSeverityWeight(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "error" => 3,
            "warning" => 2,
            "info" => 1,
            _ => 0
        };
    }
}
