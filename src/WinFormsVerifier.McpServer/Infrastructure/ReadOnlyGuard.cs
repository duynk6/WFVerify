using System.Text.RegularExpressions;
using WinFormsVerifier.Models;

namespace WinFormsVerifier.Infrastructure;

/// <summary>
/// Chế độ chỉ-đọc cho môi trường production: chặn mọi thao tác có thể ghi vào dữ liệu thật.
/// Bật bằng biến môi trường WFVERIFY_READONLY (1/true/yes/on).
/// Danh sách từ khoá nguy hiểm dùng cho wf_invoke lấy từ WFVERIFY_READONLY_BLOCKLIST
/// (phân tách bằng ';'), mặc định là các nhãn nút ghi dữ liệu phổ biến trong app tiếng Việt.
/// </summary>
public static class ReadOnlyGuard
{
    public const string EnabledVariable = "WFVERIFY_READONLY";
    public const string BlocklistVariable = "WFVERIFY_READONLY_BLOCKLIST";

    public static readonly string[] DefaultBlocklist =
    {
        "Ghi", "Lưu", "Xóa", "Xoá", "Cập nhật", "Duyệt",
        "Save", "Delete", "Update", "Insert", "Submit", "Apply"
    };

    public static bool IsEnabled => IsTruthy(Environment.GetEnvironmentVariable(EnabledVariable));

    public static IReadOnlyList<string> Blocklist
    {
        get
        {
            var raw = Environment.GetEnvironmentVariable(BlocklistVariable);
            if (string.IsNullOrWhiteSpace(raw)) return DefaultBlocklist;

            var parsed = raw.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return parsed.Length > 0 ? parsed : DefaultBlocklist;
        }
    }

    public static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
    }

    /// <summary>Từ khoá nguy hiểm khớp nhãn control, so khớp theo RANH GIỚI TỪ nên "Ghi" không dính "Nghiên cứu".</summary>
    public static string? MatchBlockedKeyword(string? label, IEnumerable<string> blocklist)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        foreach (var keyword in blocklist)
        {
            if (string.IsNullOrWhiteSpace(keyword)) continue;

            var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(keyword.Trim())}(?![\p{{L}}\p{{N}}])";
            if (Regex.IsMatch(label, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return keyword.Trim();
            }
        }

        return null;
    }

    /// <summary>Chặn hoàn toàn thao tác ghi (wf_set_value, wf_grid_set_cell) khi ở chế độ chỉ-đọc.</summary>
    public static void EnsureWriteAllowed(string toolName, string targetLabel)
    {
        if (!IsEnabled) return;

        throw new ToolException(
            ErrorCode.ReadOnlyMode,
            $"'{toolName}' bị chặn: server đang ở chế độ chỉ-đọc ({EnabledVariable}=1), không được ghi vào '{targetLabel}'.",
            $"Đây là cơ chế bảo vệ dữ liệu production. Bỏ biến môi trường {EnabledVariable} rồi khởi động lại server nếu thật sự cần ghi.");
    }

    /// <summary>Chỉ chặn wf_invoke khi nhãn control khớp danh sách từ khoá ghi dữ liệu.</summary>
    public static void EnsureInvokeAllowed(string targetLabel)
    {
        if (!IsEnabled) return;

        var keyword = MatchBlockedKeyword(targetLabel, Blocklist);
        if (keyword == null) return;

        throw new ToolException(
            ErrorCode.ReadOnlyMode,
            $"Không click được '{targetLabel}': server đang ở chế độ chỉ-đọc ({EnabledVariable}=1) và nhãn control khớp từ khoá ghi dữ liệu '{keyword}'.",
            $"Nếu nút này thật sự an toàn, chỉnh {BlocklistVariable} (danh sách phân tách bằng ';') hoặc tắt {EnabledVariable} rồi khởi động lại server.",
            details: new { BlockedKeyword = keyword, Blocklist });
    }
}
