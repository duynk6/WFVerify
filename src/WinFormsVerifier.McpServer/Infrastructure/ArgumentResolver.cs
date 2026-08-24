using System.Text.Json;
using System.Text.RegularExpressions;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Infrastructure;

/// <summary>
/// Giải placeholder trong tham số dòng lệnh của wf_launch_app để KHÔNG phải viết mật khẩu
/// thẳng vào lời gọi tool:
///   ${env:TEN_BIEN}            -> giá trị biến môi trường của server
///   ${file:C:\path\creds.json#key.con}  -> đọc khoá trong file JSON (đường dẫn phân tách bằng '.')
///   ${file:C:\path\token.txt}  -> toàn bộ nội dung file (đã trim)
/// Mọi đường dẫn đều đi qua bộ kiểm tra whitelist (PathGuard) trước khi đọc.
/// </summary>
public static class ArgumentResolver
{
    private static readonly Regex PlaceholderPattern =
        new(@"\$\{(env|file):([^}]+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string[] ResolveAll(IEnumerable<string> arguments, Func<string, string>? pathValidator = null)
    {
        return arguments.Select(a => Resolve(a, pathValidator)).ToArray();
    }

    public static string Resolve(string argument, Func<string, string>? pathValidator = null)
    {
        if (string.IsNullOrEmpty(argument) || !argument.Contains("${", StringComparison.Ordinal))
        {
            return argument;
        }

        return PlaceholderPattern.Replace(argument, match =>
        {
            var kind = match.Groups[1].Value;
            var body = match.Groups[2].Value.Trim();

            return kind switch
            {
                "env" => ResolveEnv(body),
                "file" => ResolveFile(body, pathValidator),
                _ => match.Value
            };
        });
    }

    private static string ResolveEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (value == null)
        {
            throw new ToolException(
                ErrorCode.Internal,
                $"Không tìm thấy biến môi trường '{name}' được tham chiếu bởi ${{env:{name}}}.",
                "Đặt biến này trong cấu hình MCP server (mục 'env' của .mcp.json) rồi khởi động lại client.");
        }

        return value;
    }

    private static string ResolveFile(string spec, Func<string, string>? pathValidator)
    {
        var hashIdx = spec.IndexOf('#');
        var rawPath = hashIdx >= 0 ? spec[..hashIdx].Trim() : spec;
        var keyPath = hashIdx >= 0 ? spec[(hashIdx + 1)..].Trim() : null;

        var validate = pathValidator ?? (p => PathGuard.ValidateAndNormalize(p, "arguments"));
        var fullPath = validate(rawPath);

        if (!File.Exists(fullPath))
        {
            throw new ToolException(
                ErrorCode.PathDenied,
                $"Không tìm thấy file '{fullPath}' được tham chiếu bởi ${{file:...}}.");
        }

        var content = File.ReadAllText(fullPath);
        if (string.IsNullOrEmpty(keyPath))
        {
            return content.Trim();
        }

        return ExtractJsonValue(content, keyPath, fullPath);
    }

    public static string ExtractJsonValue(string json, string keyPath, string sourceName = "(json)")
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ToolException(
                ErrorCode.Internal,
                $"File '{sourceName}' không phải JSON hợp lệ nên không đọc được khoá '{keyPath}': {ex.Message}");
        }

        using (doc)
        {
            var current = doc.RootElement;
            foreach (var key in keyPath.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(key, out var next))
                {
                    throw new ToolException(
                        ErrorCode.Internal,
                        $"Không tìm thấy khoá '{keyPath}' trong file '{sourceName}' (dừng ở '{key}').",
                        "Kiểm tra lại cấu trúc JSON; đường dẫn khoá phân tách bằng dấu chấm, ví dụ 'sql.password'.");
                }

                current = next;
            }

            return current.ValueKind == JsonValueKind.String
                ? current.GetString() ?? string.Empty
                : current.GetRawText();
        }
    }

    /// <summary>
    /// Đọc danh sách tham số từ file: JSON array of string, hoặc file text mỗi dòng một tham số
    /// (bỏ qua dòng trống và dòng bắt đầu bằng '#').
    /// </summary>
    public static string[] ParseArgumentsFile(string content, string sourceName = "(file)")
    {
        var trimmed = content.TrimStart();

        if (trimmed.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                return doc.RootElement.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : e.GetRawText())
                    .ToArray();
            }
            catch (JsonException ex)
            {
                throw new ToolException(
                    ErrorCode.Internal,
                    $"File tham số '{sourceName}' bắt đầu bằng '[' nhưng không phải mảng JSON hợp lệ: {ex.Message}",
                    "Dùng dạng [\"--user\",\"sa\"] hoặc file text mỗi dòng một tham số.");
            }
        }

        return content
            .Split('\n')
            .Select(l => l.Trim().TrimEnd('\r'))
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToArray();
    }
}
