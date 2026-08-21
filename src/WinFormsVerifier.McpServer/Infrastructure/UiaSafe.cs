using System.Drawing;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace WinFormsVerifier.Infrastructure;

/// <summary>
/// Đọc property UIA một cách an toàn.
/// FlaUI expose shortcut (element.AutomationId, element.Name, ...) trỏ thẳng vào
/// <c>Properties.X.Value</c>, và getter này NÉM <c>PropertyNotSupportedException</c>
/// khi provider không cung cấp property (điển hình: MenuStrip/ToolStrip item không có
/// AutomationId [#30011]). Toàn bộ code duyệt cây phải dùng các extension dưới đây
/// (dựa trên <c>ValueOrDefault</c> + try/catch cho lỗi COM) thay vì shortcut.
/// </summary>
public static class UiaSafe
{
    private static T Get<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch
        {
            return fallback;
        }
    }

    public static string SafeAutomationId(this AutomationElement e)
        => Get(() => e.Properties.AutomationId.ValueOrDefault ?? string.Empty, string.Empty);

    public static string SafeName(this AutomationElement e)
        => Get(() => e.Properties.Name.ValueOrDefault ?? string.Empty, string.Empty);

    public static string SafeClassName(this AutomationElement e)
        => Get(() => e.Properties.ClassName.ValueOrDefault ?? string.Empty, string.Empty);

    public static string SafeHelpText(this AutomationElement e)
        => Get(() => e.Properties.HelpText.ValueOrDefault ?? string.Empty, string.Empty);

    public static ControlType SafeControlType(this AutomationElement e)
        => Get(() => e.Properties.ControlType.ValueOrDefault, ControlType.Custom);

    public static string SafeControlTypeName(this AutomationElement e)
        => e.SafeControlType().ToString();

    public static bool SafeIsEnabled(this AutomationElement e)
        => Get(() => e.Properties.IsEnabled.ValueOrDefault, true);

    public static bool SafeIsOffscreen(this AutomationElement e)
        => Get(() => e.Properties.IsOffscreen.ValueOrDefault, false);

    public static IntPtr SafeNativeWindowHandle(this AutomationElement e)
        => Get(() => e.Properties.NativeWindowHandle.ValueOrDefault, IntPtr.Zero);

    public static Rectangle SafeBoundingRectangle(this AutomationElement e)
        => Get(() => e.Properties.BoundingRectangle.ValueOrDefault, Rectangle.Empty);

    /// <summary>
    /// Control có phải ô mật khẩu không (WinForms TextBox.PasswordChar / UseSystemPasswordChar).
    /// UIA CHẶN ValuePattern trên ô mật khẩu: SetValue không có tác dụng và đọc lại trả
    /// "Access denied". Bắt buộc phải nhập bằng bàn phím.
    /// </summary>
    public static bool SafeIsPassword(this AutomationElement e)
        => Get(() => e.FrameworkAutomationElement.IsPassword.ValueOrDefault, false);

    public static bool SafeHasKeyboardFocus(this AutomationElement e)
        => Get(() => e.Properties.HasKeyboardFocus.ValueOrDefault, false);

    /// <summary>Nhãn dễ đọc cho thông báo lỗi: name -> automationId -> controlType.</summary>
    public static string SafeLabel(this AutomationElement e)
    {
        var name = e.SafeName();
        if (!string.IsNullOrWhiteSpace(name)) return name;

        var id = e.SafeAutomationId();
        if (!string.IsNullOrWhiteSpace(id)) return id;

        return e.SafeControlTypeName();
    }
}
