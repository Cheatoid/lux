using System.Text.Json;
using System.Text.Json.Serialization;
using Tomlyn;

namespace Lux.PackageManager;

/// <summary>
/// Resolved state of the dependency graph, persisted as <c>lux.lock</c>. Entries are
/// sorted by name so the file is reproducible.
/// </summary>
public sealed class Lockfile
{
    public int Version { get; set; } = 1;

    public List<LockedPackage> Packages { get; set; } = [];

    private static readonly TomlSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        IndentSize = 4,
        DefaultIgnoreCondition = TomlIgnoreCondition.WhenWritingNull
    };

    public static Lockfile? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return TomlSerializer.Deserialize<Lockfile>(File.ReadAllText(path), Options);
        }
        catch
        {
            return null;
        }
    }

    public void Save(string path)
    {
        Packages = Packages.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        foreach (var p in Packages)
            p.Deps = p.Deps.OrderBy(d => d, StringComparer.Ordinal).ToList();

        var text = TomlSerializer.Serialize(this, Options);
        File.WriteAllText(path, text);
    }

    public LockedPackage? Find(string name) =>
        Packages.FirstOrDefault(p => p.Name == name);
}

/// <summary>
/// One entry in the lockfile. All fields are populated at resolve time.
/// </summary>
public sealed class LockedPackage
{
    public string Name { get; set; } = "";

    /// <summary>Original spec as written in the manifest.</summary>
    public string Spec { get; set; } = "";

    /// <summary>Canonical source identifier, e.g. <c>github:foo/repo</c>.</summary>
    public string Resolved { get; set; } = "";

    /// <summary>Full commit SHA.</summary>
    public string Commit { get; set; } = "";

    /// <summary>Semver string if the ref was a semver tag.</summary>
    public string? Version { get; set; }

    /// <summary>Content hash of the package contents, <c>sha256-&lt;hex&gt;</c>.</summary>
    public string Integrity { get; set; } = "";

    /// <summary>Optional subdir within the repo that is the package root.</summary>
    public string? Subdir { get; set; }

    /// <summary>Dependency names — resolved packages are looked up by name in the same lockfile.</summary>
    public List<string> Deps { get; set; } = [];
}
