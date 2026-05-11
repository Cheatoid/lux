using Lux.Diagnostics;

namespace Lux.IR;

public class IRScript(NodeID id, TextSpan span) : Node(id, span)
{
    public List<Stmt> Body { get; init; } = [];
    public ReturnStmt? Return { get; set; }
}
