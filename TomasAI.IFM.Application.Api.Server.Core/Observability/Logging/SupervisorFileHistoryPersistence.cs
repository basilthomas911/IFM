using System.Text.Json;
using TomasAI.IFM.Domain.Supervisor.Shared.Health.ReadModels;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

namespace TomasAI.IFM.Application.Api.Server.Core.Observability.Logging;

/// <summary>Persists the bounded minute-level Supervisor rollup independently of actor and polling threads.</summary>
public sealed class SupervisorFileHistoryPersistence(IHostEnvironment environment) : ISupervisorHistoryPersistence
{
    const int RetentionLimit = 10_080;
    const long MaximumFileBytes = 32L * 1024 * 1024;
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    readonly SemaphoreSlim _gate = new(1, 1);
    readonly string _path = Path.Combine(environment.ContentRootPath, "Logs", "actor-health-history.jsonl");
    int _appendsSinceCompaction;

    public async ValueTask AppendAsync(SupervisorHealthHistoryPoint point, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.AppendAllTextAsync(_path, JsonSerializer.Serialize(point, JsonOptions) + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
            if (Interlocked.Increment(ref _appendsSinceCompaction) >= 60
                || new FileInfo(_path).Length > MaximumFileBytes)
            {
                await CompactAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Exchange(ref _appendsSinceCompaction, 0);
            }
        }
        finally { _gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadAsync(
        DateTime fromUtc, DateTime toUtc, int maximumCount, CancellationToken cancellationToken)
    {
        if (fromUtc > toUtc || maximumCount <= 0 || !File.Exists(_path)) return [];
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var values = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
            return [.. values.Where(value => value.ObservedUtc >= fromUtc && value.ObservedUtc <= toUtc)
                .OrderByDescending(value => value.ObservedUtc).Take(Math.Min(maximumCount, RetentionLimit))];
        }
        finally { _gate.Release(); }
    }

    async Task CompactAsync(CancellationToken cancellationToken)
    {
        var values = (await ReadAllAsync(cancellationToken).ConfigureAwait(false))
            .OrderByDescending(value => value.ObservedUtc).Take(RetentionLimit).OrderBy(value => value.ObservedUtc).ToArray();
        var temporary = _path + ".compact";
        await File.WriteAllLinesAsync(temporary, values.Select(value => JsonSerializer.Serialize(value, JsonOptions)),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporary, _path, true);
    }

    async Task<List<SupervisorHealthHistoryPoint>> ReadAllAsync(CancellationToken cancellationToken)
    {
        List<SupervisorHealthHistoryPoint> values = [];
        await foreach (var line in File.ReadLinesAsync(_path, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var value = JsonSerializer.Deserialize<SupervisorHealthHistoryPoint>(line, JsonOptions);
                if (value is not null) values.Add(value);
            }
            catch (JsonException) { }
        }
        return values;
    }
}
