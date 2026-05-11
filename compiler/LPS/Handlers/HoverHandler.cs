using Lux.IR;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

using LuxSymbolKind = Lux.IR.SymbolKind;

namespace Lux.LPS.Handlers;

public sealed class HoverHandler(LuxWorkspace workspace) : HoverHandlerBase
{
    public override Task<Hover?> Handle(HoverParams request, CancellationToken ct)
    {
        var result = workspace.GetResult(request.TextDocument.Uri.ToString());
        if (result == null) return Task.FromResult<Hover?>(null);

        var line = request.Position.Line + 1;
        var col = request.Position.Character + 1;

        var hoveredNode = NodeFinder.Find(result.Hir, line, col);
        var nameRef = NodeFinder.FindNameRef(result.Hir, line, col);
        if (nameRef != null && nameRef.Sym != SymID.Invalid && result.Syms.GetByID(nameRef.Sym, out var sym))
        {
            var declaredType = sym.Type;
            var effectiveType = declaredType;
            if (hoveredNode is NameExpr ne && ne.Name == nameRef && ne.Type != TypID.Invalid)
            {
                effectiveType = ne.Type;
            }

            var typeStr = workspace.FormatType(result.Types, effectiveType);
            var kind = sym.Kind == LuxSymbolKind.Function ? "function" : "variable";
            string display;

            if (result.Types.GetByID(effectiveType, out var effTyp)
                && effTyp is InterfaceType or ClassType or EnumType)
            {
                display = workspace.FormatTypeBody(result, effTyp);
            }
            else if (sym.Kind == LuxSymbolKind.Function && result.Types.GetByID(sym.Type, out var typ) && typ is FunctionType ft)
            {
                List<Parameter>? declParams = null;
                if (sym.DeclaringNode != NodeID.Invalid && result.NodeRegistry.TryGetValue(sym.DeclaringNode, out var dn))
                {
                    declParams = dn switch
                    {
                        FunctionDecl fd => fd.Parameters,
                        LocalFunctionDecl lfd => lfd.Parameters,
                        _ => null
                    };
                }

                var paramParts = new List<string>();
                if (declParams != null)
                {
                    var ri = 0;
                    foreach (var dp in declParams)
                    {
                        if (dp.IsVararg)
                        {
                            var vaType = ft.VarargType != null
                                ? workspace.FormatType(result.Types, ft.VarargType)
                                : "any";
                            var vaName = dp.Name.Name != "..." ? dp.Name.Name : "";
                            paramParts.Add($"...{vaName}: {vaType}");
                        }
                        else
                        {
                            var pType = ri < ft.ParamTypes.Count
                                ? workspace.FormatType(result.Types, ft.ParamTypes[ri])
                                : "any";
                            var part = $"{dp.Name.Name}: {pType}";
                            if (dp.DefaultValue != null) part += " = ...";
                            paramParts.Add(part);
                            ri++;
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < ft.ParamTypes.Count; i++)
                    {
                        var s = workspace.FormatType(result.Types, ft.ParamTypes[i]);
                        var suffix = ft.DefaultParams.Contains(i) ? " = ..." : "";
                        paramParts.Add(s + suffix);
                    }
                    if (ft.IsVararg)
                    {
                        var vaType = ft.VarargType != null
                            ? workspace.FormatType(result.Types, ft.VarargType)
                            : "any";
                        paramParts.Add($"...: {vaType}");
                    }
                }

                var ret = workspace.FormatType(result.Types, ft.ReturnType);
                display = $"(function) {sym.Name}({string.Join(", ", paramParts)}) -> {ret}";
            }
            else if (effectiveType != declaredType)
            {
                var declaredStr = workspace.FormatType(result.Types, declaredType);
                display = $"({kind}) {sym.Name}: {typeStr}\n-- narrowed from {declaredStr}";
            }
            else
            {
                display = $"({kind}) {sym.Name}: {typeStr}";
            }

            var typesLine = workspace.FormatTypeReferencesLine(result, effectiveType);
            var docMarkdown = TryRenderDocFor(result, sym);
            var hoverValue = $"```lux\n{display}\n```";
            if (!string.IsNullOrEmpty(typesLine))
                hoverValue += $"\n\n{typesLine}";
            if (!string.IsNullOrEmpty(docMarkdown))
                hoverValue += $"\n\n---\n\n{docMarkdown}";

            return Task.FromResult<Hover?>(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = hoverValue
                }),
                Range = LuxWorkspace.SpanToRange(nameRef.Span)
            });
        }

        var annotation = FindAnnotationAt(result.Hir, line, col);
        if (annotation != null)
        {
            var info = workspace.GetAnnotationInfo(annotation.Name.Name);
            var display = info ?? $"@{annotation.Name.Name}";
            return Task.FromResult<Hover?>(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = $"```lux\n{display}\n```"
                }),
                Range = LuxWorkspace.SpanToRange(annotation.Span)
            });
        }

        if (hoveredNode is Expr expr && expr.Type != TypID.Invalid)
        {
            var typeStr = workspace.FormatType(result.Types, expr.Type);
            var typesLine = workspace.FormatTypeReferencesLine(result, expr.Type);
            var value = $"```lux\n{typeStr}\n```";
            if (!string.IsNullOrEmpty(typesLine)) value += $"\n\n{typesLine}";
            return Task.FromResult<Hover?>(new Hover
            {
                Contents = new MarkedStringsOrMarkupContent(new MarkupContent
                {
                    Kind = MarkupKind.Markdown,
                    Value = value
                }),
                Range = LuxWorkspace.SpanToRange(expr.Span)
            });
        }

        return Task.FromResult<Hover?>(null);
    }

    private static Annotation? FindAnnotationAt(IRScript hir, int line, int col)
    {
        Annotation? Check(List<Annotation> anns)
        {
            foreach (var a in anns)
            {
                if (a.Span.StartLn <= line && a.Span.EndLn >= line
                    && a.Span.StartCol <= col && a.Span.EndCol >= col)
                    return a;
            }
            return null;
        }

        foreach (var stmt in hir.Body)
        {
            var decl = stmt switch
            {
                ExportStmt ex => ex.Declaration,
                Decl d => d,
                _ => null,
            };
            if (decl == null) continue;

            Annotation? found = decl switch
            {
                FunctionDecl fd => Check(fd.Annotations),
                LocalFunctionDecl lfd => Check(lfd.Annotations),
                LocalDecl ld => Check(ld.Annotations),
                ClassDecl cd => Check(cd.Annotations),
                EnumDecl ed => Check(ed.Annotations),
                InterfaceDecl id => Check(id.Annotations),
                _ => null,
            };
            if (found != null) return found;

            if (decl is ClassDecl cls)
            {
                foreach (var f in cls.Fields) { found = Check(f.Annotations); if (found != null) return found; }
                foreach (var m in cls.Methods) { found = Check(m.Annotations); if (found != null) return found; }
            }
            if (decl is EnumDecl en)
            {
                foreach (var m in en.Members) { found = Check(m.Annotations); if (found != null) return found; }
            }
            if (decl is InterfaceDecl iface)
            {
                foreach (var f in iface.Fields) { found = Check(f.Annotations); if (found != null) return found; }
                foreach (var m in iface.Methods) { found = Check(m.Annotations); if (found != null) return found; }
            }
        }
        return null;
    }

    /// <summary>
    /// Looks up the <see cref="Doc.DocComment"/> attached to the declaration that
    /// owns the symbol and renders it for inclusion under the type-signature
    /// block of a hover. Walks class/interface/enum members so e.g. hovering a
    /// method picks up the comment on the method, not on the enclosing class.
    /// </summary>
    private static string TryRenderDocFor(AnalysisResult result, Symbol sym)
    {
        if (sym.DeclaringNode == NodeID.Invalid) return string.Empty;
        if (!result.NodeRegistry.TryGetValue(sym.DeclaringNode, out var node)) return string.Empty;

        Doc.DocComment? doc = node switch
        {
            Decl d => d.Doc,
            _ => null,
        };

        if (doc == null) return string.Empty;
        return Doc.DocMarkdown.Render(doc);
    }

    protected override HoverRegistrationOptions CreateRegistrationOptions(
        HoverCapability capability, ClientCapabilities clientCapabilities)
    {
        return new HoverRegistrationOptions
        {
            DocumentSelector = TextDocumentSelector.ForLanguage("lux")
        };
    }
}
