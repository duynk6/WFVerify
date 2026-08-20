using WinFormsVerifier.Models;

namespace WinFormsVerifier.Infrastructure;

public class ToolException : Exception
{
    public string Code { get; }
    public string? Hint { get; }
    public List<CandidateDto>? Candidates { get; }
    public object? Details { get; }

    public ToolException(
        string code,
        string message,
        string? hint = null,
        List<CandidateDto>? candidates = null,
        object? details = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Hint = hint;
        Candidates = candidates;
        Details = details;
    }
}
