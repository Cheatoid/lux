using Lux.Configuration;
using Lux.Diagnostics;

namespace Lux.IR;

internal partial class IRVisitor(string? filename, IDAlloc<NodeID> nodeAlloc, DiagnosticsBag diag, Config? config = null) : LuxBaseVisitor<Node>
{
    private readonly Config _config = config ?? new Config();

    public override Node VisitScript(LuxParser.ScriptContext context)
    {
        var (body, ret) = VisitBlockContent(context.block());
        return new IRScript(NewNodeID, SpanFromCtx(context))
        {
            Body = body,
            Return = ret
        };
    }
}
