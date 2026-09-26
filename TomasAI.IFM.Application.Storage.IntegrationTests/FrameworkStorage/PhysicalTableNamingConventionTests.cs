using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage;

public sealed class PhysicalTableNamingConventionTests
{
    static readonly Regex VersionedPhysicalTable = new(
        @"\b(?:CREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?|FROM\s+|INTO\s+|UPDATE\s+|JOIN\s+|DELETE\s+FROM\s+)(?:public\.)?[a-z][a-z0-9_]*_v\d+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void DbContext_physical_table_names_are_not_version_suffixed()
    {
        var storageFolder = Path.Combine(FindRepositoryRoot(), "TomasAI.IFM.Application.Storage");
        var violations = Directory
            .EnumerateFiles(storageFolder, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, index) => new { Path = path, Line = line, Number = index + 1 })
                .Where(candidate => VersionedPhysicalTable.IsMatch(candidate.Line)))
            .Select(candidate => $"{Path.GetRelativePath(storageFolder, candidate.Path)}:{candidate.Number}: {candidate.Line.Trim()}")
            .ToArray();

        Assert.Empty(violations);
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TomasAI.IFM.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
