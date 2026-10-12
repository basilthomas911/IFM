using System;
using System.Linq;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

/// <summary>Reads declaration documentation without assuming a fixed number of lines or no attributes.</summary>
internal static class DbContextConventionSource
{
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
