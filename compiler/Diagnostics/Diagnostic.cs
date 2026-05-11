namespace Lux.Diagnostics;

/// <summary>
/// The diagnostic itself with all the information about the diagnostic, such as the code, the category, the level, the message format, etc.
/// </summary>
public sealed class Diagnostic(DiagnosticLevel level, DiagnosticCategory category, DiagnosticCode code, string message, TextSpan span)
{
    /// <summary>
    /// The level of the diagnostic.
    /// </summary>
    public DiagnosticLevel Level { get; } = level;
    
    /// <summary>
    /// The category of the diagnostic.
    /// </summary>
    public DiagnosticCategory Category { get; } = category;
    
    /// <summary>
    /// The code of the diagnostic.
    /// </summary>
    public DiagnosticCode Code { get; } = code;
    
    /// <summary>
    /// The message of the diagnostic. This is the formatted message that is created by filling in the placeholders
    /// in the message format of the diagnostic code with the specific information about the diagnostic.
    /// </summary>
    public string Message { get; } = message;
    
    /// <summary>
    /// The text span of the diagnostic. This indicates the location of the diagnostic in the source code.
    /// </summary>
    public TextSpan Span { get; } = span;

    public override string ToString()
    {
        return $"{Category}#{Code} @ {Span}: {Message}";
    }
}