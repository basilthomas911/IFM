using System.Text.RegularExpressions;
using TomasAI.IFM.Application.ServerManager.Contracts;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost;

public sealed partial class SchedulerOutputService(SchedulerHostOptions options, SchedulerStore store)
{
    public async Task<TaskRunOutputPageDto> GetPageAsync(RunOutputRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Cursor < 0 || request.PageSize is < 1 or > 500)
        {
            throw new SchedulerValidationException("Output cursor must be non-negative and page size must be from 1 through 500.");
        }

        var location = await store.GetRunOutputLocationAsync(request.RunId, request.Stream, cancellationToken);
        if (!location.Retained)
        {
            return new TaskRunOutputPageDto(request.RunId, request.Stream, [], request.Cursor, true, location.Truncated, false);
        }

        var path = EnsureBelowRoot(options.TaskRunRoot, location.Path);
        if (!File.Exists(path))
        {
            return new TaskRunOutputPageDto(request.RunId, request.Stream, [], request.Cursor, true, location.Truncated, true);
        }

        var lines = new List<TaskOutputLineDto>();
        long sequence = 0;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, true);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (sequence >= request.Cursor && lines.Count < request.PageSize)
            {
                lines.Add(new TaskOutputLineDto(sequence, Redact(line)));
            }

            sequence++;
            if (lines.Count == request.PageSize)
            {
                break;
            }
        }

        var next = request.Cursor + lines.Count;
        return new TaskRunOutputPageDto(
            request.RunId,
            request.Stream,
            lines,
            next,
            lines.Count < request.PageSize,
            location.Truncated,
            true);
    }

    public static string Redact(string value)
        => SecretPattern().Replace(value, match => $"{match.Groups[1].Value}=<redacted>");

    private static string EnsureBelowRoot(string root, string path)
    {
        var canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var canonicalPath = Path.GetFullPath(path);
        if (!canonicalPath.StartsWith(canonicalRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new SchedulerValidationException("Stored output path escapes the configured task-run root.");
        }

        return canonicalPath;
    }

    [GeneratedRegex(@"(?i)\b(password|token|secret|apikey|api_key)\s*=\s*[^\s;]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretPattern();
}
