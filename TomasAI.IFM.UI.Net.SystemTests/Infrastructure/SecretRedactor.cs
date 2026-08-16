using System.Text.RegularExpressions;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed partial class SecretRedactor(IEnumerable<string?> secrets)
{
    readonly string[] _secrets = secrets
        .Where(secret => !string.IsNullOrWhiteSpace(secret))
        .Select(secret => secret!)
        .Distinct(StringComparer.Ordinal)
        .OrderByDescending(secret => secret.Length)
        .ToArray();

    public string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = value;
        foreach (var secret in _secrets)
            redacted = redacted.Replace(secret, "[REDACTED]", StringComparison.Ordinal);

        return AssignmentRegex().Replace(redacted, match => $"{match.Groups[1].Value}[REDACTED]");
    }

    [GeneratedRegex("(?i)((?:api[-_]?key|authorization|password|token|secret)\\s*[:=]\\s*(?:bearer\\s+)?)[^\\s,;\\\"]+")]
    private static partial Regex AssignmentRegex();
}
