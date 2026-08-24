namespace WinFormsVerifier.Models;

public static class ErrorCode
{
    public const string NoSession = "NO_SESSION";
    public const string AppExited = "APP_EXITED";
    public const string WindowNotFound = "WINDOW_NOT_FOUND";
    public const string ElementNotFound = "ELEMENT_NOT_FOUND";
    public const string Ambiguous = "AMBIGUOUS";
    public const string PatternUnsupported = "PATTERN_UNSUPPORTED";
    public const string Timeout = "TIMEOUT";
    public const string BlockedByModal = "BLOCKED_BY_MODAL";
    public const string PathDenied = "PATH_DENIED";
    public const string ReadOnlyMode = "READONLY_MODE";
    public const string Internal = "INTERNAL";
}
