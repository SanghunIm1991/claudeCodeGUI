namespace ClaudeCodeGui.Services;

public record ArtifactEntry(string Name, string RelativePath, bool IsDirectory);

/// <summary>
/// Issueの対象プロジェクトディレクトリ配下のファイルを一覧・読み書きする。
/// 対象ディレクトリの外へは一切アクセスできないようパスを検証する。
/// </summary>
public class ArtifactService
{
    public List<ArtifactEntry> List(string rootPath, string relativeDir)
    {
        var dir = ResolveWithinRoot(rootPath, relativeDir);
        if (!Directory.Exists(dir)) return new List<ArtifactEntry>();

        var entries = new List<ArtifactEntry>();
        foreach (var d in Directory.EnumerateDirectories(dir).OrderBy(x => x))
        {
            if (Path.GetFileName(d) is ".git" or "bin" or "obj" or "node_modules") continue;
            entries.Add(new ArtifactEntry(Path.GetFileName(d), ToRelative(rootPath, d), true));
        }
        foreach (var f in Directory.EnumerateFiles(dir).OrderBy(x => x))
        {
            entries.Add(new ArtifactEntry(Path.GetFileName(f), ToRelative(rootPath, f), false));
        }
        return entries;
    }

    public string ReadFile(string rootPath, string relativePath)
    {
        var path = ResolveWithinRoot(rootPath, relativePath);
        return File.ReadAllText(path);
    }

    public void WriteFile(string rootPath, string relativePath, string content)
    {
        var path = ResolveWithinRoot(rootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string ToRelative(string rootPath, string fullPath)
    {
        var root = Path.GetFullPath(rootPath);
        var rel = Path.GetRelativePath(root, fullPath);
        return rel.Replace('\\', '/');
    }

    private static string ResolveWithinRoot(string rootPath, string relativePath)
    {
        var root = Path.GetFullPath(rootPath);
        var combined = Path.GetFullPath(Path.Combine(root, relativePath ?? ""));
        if (combined != root && !combined.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("対象プロジェクトディレクトリの外にはアクセスできません。");
        }
        return combined;
    }
}
