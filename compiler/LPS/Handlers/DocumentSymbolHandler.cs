using Lux.IR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

using LspSymbolKind = OmniSharp.Extensions.LanguageServer.Protocol.Models.SymbolKind;

namespace Lux.LPS.Handlers;

public sealed class DocumentSymbolHandler(LuxWorkspace workspace) : DocumentSymbolHandlerBase
{
    public override Task<SymbolInformationOrDocumentSymbolContainer?> Handle(
        DocumentSymbolParams request, CancellationToken ct)
    {
        var result = workspace.GetResult(request.TextDocument.Uri.ToString());
        if (result == null)
            return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(null);

        var symbols = new List<SymbolInformationOrDocumentSymbol>();
        CollectSymbols(result.Hir.Body, symbols);
        return Task.FromResult<SymbolInformationOrDocumentSymbolContainer?>(
            new SymbolInformationOrDocumentSymbolContainer(symbols));
    }

    private void CollectSymbols(List<Stmt> stmts, List<SymbolInformationOrDocumentSymbol> symbols)
    {
        foreach (var stmt in stmts)
        {
            switch (stmt)
            {
                case FunctionDecl fd:
                {
                    var name = string.Join(".", fd.NamePath.Select(n => n.Name));
                    if (fd.MethodName != null) name += ":" + fd.MethodName.Name;
                    symbols.Add(new DocumentSymbol
                    {
                        Name = name,
                        Kind = LspSymbolKind.Function,
                        Range = LuxWorkspace.SpanToRange(fd.Span),
                        SelectionRange = LuxWorkspace.SpanToRange(fd.NamePath[0].Span)
                    });
                    break;
                }
                case LocalFunctionDecl lfd:
                    symbols.Add(new DocumentSymbol
                    {
                        Name = lfd.Name.Name,
                        Kind = LspSymbolKind.Function,
                        Range = LuxWorkspace.SpanToRange(lfd.Span),
                        SelectionRange = LuxWorkspace.SpanToRange(lfd.Name.Span)
                    });
                    break;
                case LocalDecl ld:
                    foreach (var v in ld.Variables)
                    {
                        symbols.Add(new DocumentSymbol
                        {
                            Name = v.Name.Name,
                            Kind = LspSymbolKind.Variable,
                            Range = LuxWorkspace.SpanToRange(v.Span),
                            SelectionRange = LuxWorkspace.SpanToRange(v.Name.Span)
                        });
                    }
                    break;
                case ClassDecl cd:
                {
                    var children = new List<DocumentSymbol>();
                    if (cd.Constructor != null)
                    {
                        children.Add(new DocumentSymbol
                        {
                            Name = "constructor",
                            Kind = LspSymbolKind.Constructor,
                            Range = LuxWorkspace.SpanToRange(cd.Constructor.Span),
                            SelectionRange = LuxWorkspace.SpanToRange(cd.Constructor.Span)
                        });
                    }
                    foreach (var method in cd.Methods)
                    {
                        var label = method.IsOperator && method.OperatorSymbol != null
                            ? "operator " + method.OperatorSymbol
                            : method.Name.Name;
                        children.Add(new DocumentSymbol
                        {
                            Name = label,
                            Kind = method.IsOperator ? LspSymbolKind.Operator : LspSymbolKind.Method,
                            Range = LuxWorkspace.SpanToRange(method.Span),
                            SelectionRange = LuxWorkspace.SpanToRange(method.Name.Span)
                        });
                    }
                    foreach (var field in cd.Fields)
                    {
                        children.Add(new DocumentSymbol
                        {
                            Name = field.Name.Name,
                            Kind = LspSymbolKind.Field,
                            Range = LuxWorkspace.SpanToRange(field.Span),
                            SelectionRange = LuxWorkspace.SpanToRange(field.Name.Span)
                        });
                    }
                    foreach (var accessor in cd.Accessors)
                    {
                        children.Add(new DocumentSymbol
                        {
                            Name = (accessor.Kind == AccessorKind.Getter ? "get " : "set ") + accessor.Name.Name,
                            Kind = LspSymbolKind.Property,
                            Range = LuxWorkspace.SpanToRange(accessor.Span),
                            SelectionRange = LuxWorkspace.SpanToRange(accessor.Name.Span)
                        });
                    }
                    symbols.Add(new DocumentSymbol
                    {
                        Name = cd.Name.Name,
                        Kind = LspSymbolKind.Class,
                        Range = LuxWorkspace.SpanToRange(cd.Span),
                        SelectionRange = LuxWorkspace.SpanToRange(cd.Name.Span),
                        Children = new Container<DocumentSymbol>(children)
                    });
                    break;
                }
                case InterfaceDecl id:
                    symbols.Add(new DocumentSymbol
                    {
                        Name = id.Name.Name,
                        Kind = LspSymbolKind.Interface,
                        Range = LuxWorkspace.SpanToRange(id.Span),
                        SelectionRange = LuxWorkspace.SpanToRange(id.Name.Span)
                    });
                    break;
                case ExportStmt exp:
                    CollectSymbols([exp.Declaration], symbols);
                    break;
            }
        }
    }

    protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(
        DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
    {
        return new DocumentSymbolRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("lux")
        };
    }
}
