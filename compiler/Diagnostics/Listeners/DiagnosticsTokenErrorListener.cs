using Antlr4.Runtime;

namespace Lux.Diagnostics;

/// <summary>
/// Represents a custom error listener for the ANTLR parsing pipeline to intercept and handle syntax errors during the parsing process.
/// </summary>
internal sealed class DiagnosticsTokenErrorListener(DiagnosticsBag diag, string? filename) : BaseErrorListener
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
        string msg, RecognitionException e)
    {
        if (offendingSymbol is CommonToken { Type: LuxParser.Eof })
        {
            diag.Report(TextSpan.Of(offendingSymbol, filename), DiagnosticCode.ErrUnexpectedEOF);
        }
        else
        {
            diag.Report(TextSpan.Of(offendingSymbol, filename), DiagnosticCode.ErrUnexpectedToken, msg);
        }
    }
}