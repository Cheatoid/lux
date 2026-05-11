using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;

namespace Lux.PackageManager;

/// <summary>
/// Bridges the bare-clone cache, the content-addressed store, and <see cref="GitRunner"/>.
/// Responsible for fetching, ref resolution, snapshot extraction and integrity hashing.
/// </summary>
public sealed class GitFetcher
{
    /// <summary>
    /// Ensures a bare clone exists in the cache and is up to date. Returns the bare-clone path.
    /// </summary>
    public async Task<string> EnsureBareCloneAsync(PackageSpec spec)
    {
        if (spec.Kind != SpecKind.Git)
            throw new InvalidOperationException("EnsureBareCloneAsync requires a git spec");

        var barePath = LuxHome.BareClonePath(spec.Host!, spec.Owner!, spec.Repo!);
        var parent = Path.GetDirectoryName(barePath)!;
        Directory.CreateDirectory(parent);

        var headExists = File.Exists(Path.Combine(barePath, "HEAD"));
        if (headExists)
        {
            var (ec, _, err) = await GitRunner.RunAsync(barePath, "fetch", "--all", "--tags", "--quiet", "--prune");
            if (ec != 0) throw new PackageManagerException($"git fetch failed for {spec.CloneUrl}: {err.Trim()}");
        }
        else
        {
            if (Directory.Exists(barePath))
            {
                try { Directory.Delete(barePath, recursive: true); }
                catch { /* best effort */ }
            }
            var (ec, _, err) = await GitRunner.RunAsync(null, "clone", "--bare", "--quiet", spec.CloneUrl!, barePath);
            if (ec != 0) throw new PackageManagerException($"git clone failed for {spec.CloneUrl}: {err.Trim()}");
        }

        return barePath;
    }

    /// <summary>
    /// Returns the list of tags as reported by <c>git tag --list</c>.
    /// </summary>
    public async Task<List<string>> ListTagsAsync(string barePath)
    {
        var (ec, @out, err) = await GitRunner.RunAsync(barePath, "tag", "--list");
        if (ec != 0) throw new PackageManagerException($"git tag --list failed: {err.Trim()}");
        return @out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    /// <summary>
    /// Resolves a ref string (tag / branch / short SHA / full SHA) to a full commit SHA.
    /// </summary>
    public async Task<string> ResolveRefAsync(string barePath, string @ref)
    {
        var (ec, @out, _) = await GitRunner.RunAsync(barePath, "rev-parse", "--verify", @ref + "^{commit}");
        if (ec == 0) return @out.Trim();

        (ec, @out, _) = await GitRunner.RunAsync(barePath, "rev-parse", "--verify", "refs/tags/" + @ref + "^{commit}");
        if (ec == 0) return @out.Trim();

        (ec, @out, _) = await GitRunner.RunAsync(barePath, "rev-parse", "--verify", "refs/heads/" + @ref + "^{commit}");
        if (ec == 0) return @out.Trim();

        throw new PackageManagerException($"cannot resolve ref '{@ref}'");
    }

    /// <summary>
    /// Returns the default branch commit by asking origin. Falls back to HEAD of the bare clone.
    /// </summary>
    public async Task<string> ResolveDefaultBranchAsync(string barePath)
    {
        var (ec, @out, _) = await GitRunner.RunAsync(barePath, "rev-parse", "--verify", "HEAD^{commit}");
        if (ec == 0) return @out.Trim();

        foreach (var candidate in new[] { "main", "master" })
        {
            (ec, @out, _) = await GitRunner.RunAsync(barePath, "rev-parse", "--verify", "refs/heads/" + candidate + "^{commit}");
            if (ec == 0) return @out.Trim();
        }
        throw new PackageManagerException("cannot determine default branch");
    }

    /// <summary>
    /// Extracts a commit snapshot from the bare clone into the store. Idempotent — if the
    /// destination already exists, returns immediately.
    /// </summary>
    public async Task EnsureSnapshotAsync(string barePath, string commit, string destDir, string? subdir = null)
    {
        if (Directory.Exists(destDir) && Directory.EnumerateFileSystemEntries(destDir).Any())
            return;

        Directory.CreateDirectory(LuxHome.TmpRoot);
        var tarPath = Path.Combine(LuxHome.TmpRoot, $"lux-{Guid.NewGuid():N}.tar");
        var stageDir = Path.Combine(LuxHome.TmpRoot, $"lux-stage-{Guid.NewGuid():N}");

        try
        {
            var args = new List<string> { "archive", "--format=tar", "-o", tarPath, commit };
            if (!string.IsNullOrEmpty(subdir))
                args.Add(subdir.TrimStart('/'));

            var (ec, _, err) = await GitRunner.RunAsync(barePath, args.ToArray());
            if (ec != 0) throw new PackageManagerException($"git archive failed: {err.Trim()}");

            Directory.CreateDirectory(stageDir);
            TarFile.ExtractToDirectory(tarPath, stageDir, overwriteFiles: true);

            // Move into destination atomically. If destDir exists (empty), place contents into it.
            var destParent = Path.GetDirectoryName(destDir)!;
            Directory.CreateDirectory(destParent);

            if (Directory.Exists(destDir))
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(stageDir))
                {
                    var targetPath = Path.Combine(destDir, Path.GetFileName(entry));
                    if (Directory.Exists(entry)) MoveDirectory(entry, targetPath);
                    else File.Move(entry, targetPath, overwrite: true);
                }
            }
            else
            {
                Directory.Move(stageDir, destDir);
                stageDir = null!;
            }
        }
        finally
        {
            try { if (File.Exists(tarPath)) File.Delete(tarPath); } catch { }
            try { if (stageDir != null && Directory.Exists(stageDir)) Directory.Delete(stageDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Computes a deterministic SHA-256 over the package contents: relative paths in sorted order
    /// followed by file bytes. Prefixed with <c>sha256-</c>.
    /// </summary>
    public static string ComputeIntegrity(string root)
    {
        using var sha = SHA256.Create();
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(p => p.Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();

        var buffer = new byte[81920];
        foreach (var f in files)
        {
            var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
            var relBytes = Encoding.UTF8.GetBytes(rel + "\0");
            sha.TransformBlock(relBytes, 0, relBytes.Length, null, 0);

            using var fs = File.OpenRead(f);
            int read;
            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                sha.TransformBlock(buffer, 0, read, null, 0);
            sha.TransformBlock([0], 0, 1, null, 0);
        }

        sha.TransformFinalBlock([], 0, 0);
        return "sha256-" + Convert.ToHexString(sha.Hash!).ToLowerInvariant();
    }

    private static void MoveDirectory(string source, string target)
    {
        if (!Directory.Exists(target))
        {
            Directory.Move(source, target);
            return;
        }
        foreach (var dir in Directory.GetDirectories(source))
        {
            var name = Path.GetFileName(dir);
            MoveDirectory(dir, Path.Combine(target, name));
        }
        foreach (var file in Directory.GetFiles(source))
        {
            var name = Path.GetFileName(file);
            File.Move(file, Path.Combine(target, name), overwrite: true);
        }
        try { Directory.Delete(source, recursive: false); } catch { }
    }
}
