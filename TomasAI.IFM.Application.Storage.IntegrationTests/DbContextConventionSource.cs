using System;
using System.Linq;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

/// <summary>Reads declaration documentation without assuming a fixed number of lines or no attributes.</summary>
internal static class DbContextConventionSource
{
    // Builds can live on E: while the reviewed source remains on C:.
    internal static string RepositoryRoot => FindRepositoryRoot();

    static string FindRepositoryRoot([System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        for (var directory = new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(source)!);
             directory is not null; directory = directory.Parent)
            if (System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
                return directory.FullName;
        throw new System.IO.DirectoryNotFoundException("Could not locate the repository containing the compiled test source.");
    }

    internal static bool HasXmlSummary(string[] lines, int declarationIndex)
    {
        var preceding = lines.Take(declarationIndex).Reverse()
            .TakeWhile(line => string.IsNullOrWhiteSpace(line)
                || line.TrimStart().StartsWith("///", StringComparison.Ordinal)
                || line.TrimStart().StartsWith("[", StringComparison.Ordinal))
            .Where(line => line.TrimStart().StartsWith("///", StringComparison.Ordinal)).ToArray();
        return preceding.Any(line => line.Contains("<summary>", StringComparison.Ordinal))
            && preceding.Any(line => line.Contains("</summary>", StringComparison.Ordinal));
    }
}
