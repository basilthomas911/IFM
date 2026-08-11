using System.Text.RegularExpressions;

namespace TomasAI.IFM.UI.Presentation.UnitTests.Architecture;

internal static class SolutionSource
{
    static readonly Lazy<DirectoryInfo> SolutionRoot = new(FindSolutionRoot);

    public static string RootPath => SolutionRoot.Value.FullName;

    public static IReadOnlyList<string> GetSourceFiles(params string[] projectNames)
        => projectNames
            .Select(projectName => Path.Combine(RootPath, projectName))
            .SelectMany(projectPath => Directory.EnumerateFiles(projectPath, "*.cs", SearchOption.AllDirectories))
            .Where(path => !HasDirectory(path, "bin") && !HasDirectory(path, "obj"))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static int CountMatches(IEnumerable<string> sourceFiles, string pattern)
    {
        var expression = new Regex(
            pattern,
            RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline);
        return sourceFiles.Sum(path => expression.Matches(File.ReadAllText(path)).Count);
    }

    public static IReadOnlyList<string> FindFilesWithMatches(
        IEnumerable<string> sourceFiles,
        string pattern)
    {
        var expression = new Regex(
            pattern,
            RegexOptions.CultureInvariant | RegexOptions.Multiline);
        return sourceFiles
            .Where(path => expression.IsMatch(File.ReadAllText(path)))
            .Select(GetRelativePath)
            .ToArray();
    }

    public static string GetRelativePath(string path)
        => Path.GetRelativePath(RootPath, path).Replace('\\', '/');

    static bool HasDirectory(string path, string directoryName)
        => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Contains(directoryName, StringComparer.OrdinalIgnoreCase);

    static DirectoryInfo FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate TomasAI.IFM.sln above {AppContext.BaseDirectory}.");
    }
}
