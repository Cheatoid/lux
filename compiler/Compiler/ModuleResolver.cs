using Antlr4.Runtime;
using Lux.Configuration;
using Lux.Diagnostics;
using Lux.IR;
using Lux.PackageManager;

namespace Lux.Compiler;

public enum ModuleKind
{
    LuxSource,
    Declaration,
    DeclareModule
}

public sealed class ResolvedModule
{
    public ModuleKind Kind { get; init; }
    public PreparsedFile? File { get; init; }
    public DeclareModuleDecl? DeclareModule { get; init; }
    public string? FilePath { get; init; }
}

public sealed class ModuleResolver(Config config)
{
    private readonly string _sourceRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, config.Source));
    private readonly Dictionary<string, ResolvedModule> _cache = new();

    public ResolvedModule? Resolve(string moduleName, string? importerPath,
        List<PackageContext> pkgs, DiagnosticsBag diag, IDAlloc<NodeID> nodeAlloc)
    {
        if (_cache.TryGetValue(moduleName, out var cached))
            return cached;

        var result = DoResolve(moduleName, importerPath, pkgs, diag, nodeAlloc);
        if (result != null)
            _cache[moduleName] = result;
        return result;
    }

    private ResolvedModule? DoResolve(string moduleName, string? importerPath,
        List<PackageContext> pkgs, DiagnosticsBag diag, IDAlloc<NodeID> nodeAlloc)
    {
        if (moduleName.EndsWith(".lux"))
            moduleName = moduleName[..^4];

        var found = FindDeclareModule(moduleName, pkgs);
        if (found != null) return found;

        var searchDirs = BuildSearchPaths(importerPath);

        foreach (var dir in searchDirs)
        {
            var dlux = Path.Combine(dir, moduleName + ".d.lux");
            if (File.Exists(dlux))
            {
                var file = LoadAndInject(dlux, pkgs, diag, nodeAlloc);
                if (file != null)
                    return new ResolvedModule { Kind = ModuleKind.Declaration, File = file, FilePath = dlux };
            }

            var lux = Path.Combine(dir, moduleName + ".lux");
            if (File.Exists(lux))
            {
                var file = LoadAndInject(lux, pkgs, diag, nodeAlloc);
                if (file != null)
                    return new ResolvedModule { Kind = ModuleKind.LuxSource, File = file, FilePath = lux };
            }

            var dluxIdx = Path.Combine(dir, moduleName, "init.d.lux");
            if (File.Exists(dluxIdx))
            {
                var file = LoadAndInject(dluxIdx, pkgs, diag, nodeAlloc);
                if (file != null)
                    return new ResolvedModule { Kind = ModuleKind.Declaration, File = file, FilePath = dluxIdx };
            }

            var luxIdx = Path.Combine(dir, moduleName, "init.lux");
            if (File.Exists(luxIdx))
            {
                var file = LoadAndInject(luxIdx, pkgs, diag, nodeAlloc);
                if (file != null)
                    return new ResolvedModule { Kind = ModuleKind.LuxSource, File = file, FilePath = luxIdx };
            }
        }

        return null;
    }

    private List<string> BuildSearchPaths(string? importerPath)
    {
        var paths = new List<string>();

        if (importerPath != null)
        {
            var importerDir = Path.GetDirectoryName(Path.GetFullPath(importerPath));
            if (importerDir != null) paths.Add(importerDir);
        }

        if (Directory.Exists(_sourceRoot))
            paths.Add(_sourceRoot);

        var modulesDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, InstalledPackages.ModulesDirName));
        if (Directory.Exists(modulesDir))
            paths.Add(modulesDir);

        foreach (var lib in config.Code.Libs)
        {
            var libPath = Path.IsPathRooted(lib)
                ? lib
                : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, lib));
            if (Directory.Exists(libPath))
                paths.Add(libPath);
        }

        foreach (var g in config.Globals)
        {
            var gPath = Path.IsPathRooted(g)
                ? g
                : Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, g));
            if (Directory.Exists(gPath))
                paths.Add(gPath);
        }

        return paths;
    }

    private static ResolvedModule? FindDeclareModule(string moduleName, List<PackageContext> pkgs)
    {
        foreach (var pkg in pkgs)
        {
            foreach (var file in pkg.Files)
            {
                foreach (var stmt in file.Hir.Body)
                {
                    if (stmt is DeclareModuleDecl dmd && dmd.ModuleName.Name == moduleName)
                    {
                        return new ResolvedModule
                        {
                            Kind = ModuleKind.DeclareModule,
                            DeclareModule = dmd,
                            File = file,
                            FilePath = file.Filename
                        };
                    }
                }
            }
        }
        return null;
    }

    private PreparsedFile? LoadAndInject(string filePath, List<PackageContext> pkgs,
        DiagnosticsBag diag, IDAlloc<NodeID> nodeAlloc)
    {
        foreach (var existingPkg in pkgs)
        {
            var existing = existingPkg.Files.FirstOrDefault(f => f.Filename == filePath);
            if (existing != null) return existing;
        }

        var targetPkg = pkgs.FirstOrDefault();
        if (targetPkg == null) return null;

        string source;
        try { source = File.ReadAllText(filePath); }
        catch { return null; }

        var inputStream = new AntlrInputStream(source);
        var lexer = new LuxLexer(inputStream);
        lexer.RemoveErrorListeners();
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new LuxParser(tokenStream);
        parser.RemoveErrorListeners();
        var visitor = new IRVisitor(filePath, nodeAlloc, diag, config);
        var ir = visitor.Visit(parser.script());

        if (ir is not IRScript script) return null;

        var file = new PreparsedFile(filePath, source) { Hir = script };
        targetPkg.Files.Add(file);
        return file;
    }
}
