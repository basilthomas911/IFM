using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed class G0EvidenceWriter
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    readonly SecretRedactor _redactor;

    public G0EvidenceWriter(G0Configuration configuration, SecretRedactor redactor)
    {
        _redactor = redactor;
        RunDirectory = Path.Combine(configuration.ResultsRoot, $"{configuration.RunId}-{configuration.EnvironmentName}");
        ApiLogDirectory = CreateDirectory("logs", "api-server");
        UiLogDirectory = CreateDirectory("logs", "ui");
        ScreenshotDirectory = CreateDirectory("screenshots");
        AutomationTreeDirectory = CreateDirectory("automation-trees");
        NetworkDirectory = CreateDirectory("network");
        ProcessDirectory = CreateDirectory("processes");
    }

    public string RunDirectory { get; }
    public string ApiLogDirectory { get; }
    public string UiLogDirectory { get; }
    public string ScreenshotDirectory { get; }
    public string AutomationTreeDirectory { get; }
    public string NetworkDirectory { get; }
    public string ProcessDirectory { get; }

    public async Task WriteTextAsync(string relativePath, string contents, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(RunDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, _redactor.Redact(contents), cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteResultAsync(G0RunResult result, CancellationToken cancellationToken = default)
    {
        var json = _redactor.Redact(JsonSerializer.Serialize(result, JsonOptions));
        await File.WriteAllTextAsync(Path.Combine(RunDirectory, "result.json"), json, cancellationToken).ConfigureAwait(false);

        StringBuilder summary = new();
        summary.AppendLine("# IFM UI G0 process audit");
        summary.AppendLine();
        summary.AppendLine($"- Run: `{result.RunId}`");
        summary.AppendLine($"- Environment: `{result.Environment}`");
        summary.AppendLine($"- Outcome: `{(result.Passed ? "Passed" : "Failed")}`");
        summary.AppendLine($"- Cleanup: `{(result.CleanupSucceeded ? "Succeeded" : "Failed")}`");
        summary.AppendLine();
        summary.AppendLine("| Step | Status | Actual |");
        summary.AppendLine("|---|---|---|");
        foreach (var step in result.Steps)
            summary.AppendLine($"| {step.Id} | {step.Status} | {EscapeCell(step.Actual)} |");
        await File.WriteAllTextAsync(
            Path.Combine(RunDirectory, "summary.md"),
            _redactor.Redact(summary.ToString()),
            cancellationToken).ConfigureAwait(false);
    }

    string CreateDirectory(params string[] parts)
    {
        var path = parts.Aggregate(RunDirectory, Path.Combine);
        Directory.CreateDirectory(path);
        return path;
    }

    static string EscapeCell(string value)
        => value.Replace("|", "\\|", StringComparison.Ordinal).ReplaceLineEndings(" ");
}
