using System.Text.Json;
using System.Diagnostics;
using System.Diagnostics.Metrics;
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
    static readonly Meter HistoryMeter = new("TomasAI.IFM.SupervisorHistory", "1.0");
    static readonly Histogram<double> GateWait = HistoryMeter.CreateHistogram<double>("supervisor.history.gate_wait", "ms");
    SupervisorHealthHistoryPoint[]? index;
    bool appendSeparator;
    int _appendsSinceCompaction;

    public async ValueTask AppendAsync(SupervisorHealthHistoryPoint point, CancellationToken cancellationToken)
    {
        await EnterAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            await File.AppendAllTextAsync(_path, (appendSeparator ? Environment.NewLine : "") + JsonSerializer.Serialize(point, JsonOptions) + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
            appendSeparator = false;
            // Equal timestamps retain the original file order, matching stable OrderByDescending.
            var position = 0;
            while (position < current.Length && current[position].ObservedUtc >= point.ObservedUtc) position++;
            var next = new SupervisorHealthHistoryPoint[Math.Min(current.Length + 1, RetentionLimit)];
            var prefix = Math.Min(position, next.Length);
            Array.Copy(current, next, prefix);
            if (position < next.Length)
            {
                next[position] = point;
                Array.Copy(current, position, next, position + 1, next.Length - position - 1);
            }
            Volatile.Write(ref index, next);
            if (Interlocked.Increment(ref _appendsSinceCompaction) >= 60
                || new FileInfo(_path).Length > MaximumFileBytes)
            {
                await CompactAsync(cancellationToken).ConfigureAwait(false);
                Interlocked.Exchange(ref _appendsSinceCompaction, 0);
            }
        }
        catch { Volatile.Write(ref index, null); throw; }
        finally { _gate.Release(); }
    }

    public async ValueTask<IReadOnlyList<SupervisorHealthHistoryPoint>> ReadAsync(
        DateTime fromUtc, DateTime toUtc, int maximumCount, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (fromUtc > toUtc || maximumCount <= 0) return [];
        var values = Volatile.Read(ref index);
        if (values is null)
        {
            await EnterAsync(cancellationToken).ConfigureAwait(false);
            try { values = await LoadIndexAsync(cancellationToken).ConfigureAwait(false); }
            finally { _gate.Release(); }
        }
        return [.. values.Where(value => value.ObservedUtc >= fromUtc && value.ObservedUtc <= toUtc)
            .Take(Math.Min(maximumCount, RetentionLimit))];
    }

    async Task EnterAsync(CancellationToken token)
    {
        var started = Stopwatch.GetTimestamp();
        await _gate.WaitAsync(token).ConfigureAwait(false);
        GateWait.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    async Task<SupervisorHealthHistoryPoint[]> LoadIndexAsync(CancellationToken token)
    {
        if (index is { } loaded) return loaded;
        var values = File.Exists(_path) ? await ReadAllAsync(token).ConfigureAwait(false) : [];
        if (File.Exists(_path))
        {
            using var file = File.OpenRead(_path);
            if (file.Length > 0) { file.Seek(-1, SeekOrigin.End); appendSeparator = file.ReadByte() != '\n'; }
        }
        loaded = [.. values.OrderByDescending(value => value.ObservedUtc).Take(RetentionLimit)];
        Volatile.Write(ref index, loaded);
        return loaded;
    }

    async Task CompactAsync(CancellationToken cancellationToken)
    {
        var values = (await LoadIndexAsync(cancellationToken).ConfigureAwait(false)).OrderBy(value => value.ObservedUtc).ToArray();
        var temporary = _path + ".compact";
        await File.WriteAllLinesAsync(temporary, values.Select(value => JsonSerializer.Serialize(value, JsonOptions)),
            cancellationToken).ConfigureAwait(false);
        File.Move(temporary, _path, true);
    }

    async Task<List<SupervisorHealthHistoryPoint>> ReadAllAsync(CancellationToken cancellationToken)
    {
        // Cold recovery is bounded even when a prior process died before compaction.
        var values = new PriorityQueue<(SupervisorHealthHistoryPoint Point, long Sequence), (long Ticks, long ReverseSequence)>();
        long sequence = 0;
        await foreach (var line in File.ReadLinesAsync(_path, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                var value = JsonSerializer.Deserialize<SupervisorHealthHistoryPoint>(line, JsonOptions);
                if (value is not null)
                {
                    values.Enqueue((value, sequence), (value.ObservedUtc.Ticks, -sequence));
                    sequence++;
                    if (values.Count > RetentionLimit) values.Dequeue();
                }
            }
            catch (JsonException) { }
        }
        return values.UnorderedItems.Select(item => item.Element).OrderBy(item => item.Sequence).Select(item => item.Point).ToList();
    }
}
