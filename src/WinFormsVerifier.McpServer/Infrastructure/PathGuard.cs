using WinFormsVerifier.Models;

namespace WinFormsVerifier.Infrastructure;

public static class PathGuard
{
    private static readonly Lazy<List<string>> AllowedRoots = new(LoadAllowedRoots);

    private static List<string> LoadAllowedRoots()
    {
        var env = Environment.GetEnvironmentVariable("WFVERIFY_ALLOWED_ROOTS");
        var roots = new List<string>();

        if (!string.IsNullOrWhiteSpace(env))
        {
            var parts = env.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var part in parts)
            {
                try
                {
                    roots.Add(Path.GetFullPath(part));
                }
                catch
                {
                    // ignore invalid path in env
                }
            }
        }

        if (roots.Count == 0)
        {
            roots.Add(Path.GetFullPath(Environment.CurrentDirectory));
            roots.Add(Path.GetFullPath(AppContext.BaseDirectory));

            // Search upward for solution or workspace root
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (dir.GetFiles("*.sln").Length > 0 || dir.GetDirectories(".git").Length > 0 || dir.GetFiles("plan.md").Length > 0)
                {
                    roots.Add(dir.FullName);
                    break;
                }
                dir = dir.Parent;
            }

            var curDir = new DirectoryInfo(Environment.CurrentDirectory);
            while (curDir != null)
            {
                if (curDir.GetFiles("*.sln").Length > 0 || curDir.GetDirectories(".git").Length > 0 || curDir.GetFiles("plan.md").Length > 0)
                {
                    roots.Add(curDir.FullName);
                    break;
                }
                curDir = curDir.Parent;
            }
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string ValidateAndNormalize(string path, string parameterName = "path")
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ToolException(ErrorCode.PathDenied, $"Tham số '{parameterName}' không được để trống.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            throw new ToolException(ErrorCode.PathDenied, $"Đường dẫn '{path}' không hợp lệ: {ex.Message}");
        }

        var allowed = AllowedRoots.Value.Any(root =>
            fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));

        if (!allowed)
        {
            var allowedList = string.Join("; ", AllowedRoots.Value);
            throw new ToolException(
                ErrorCode.PathDenied,
                $"Đường dẫn '{fullPath}' nằm ngoài danh sách whitelist được phép.",
                $"Chỉ cho phép các đường dẫn thuộc: [{allowedList}]. Hãy cấu hình biến môi trường WFVERIFY_ALLOWED_ROOTS nếu cần mở rộng.");
        }

        return fullPath;
    }
}
