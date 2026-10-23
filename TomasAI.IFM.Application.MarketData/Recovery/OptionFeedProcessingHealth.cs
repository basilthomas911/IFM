using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Recovery;

[MessagePackObject]
public sealed record OptionFeedProcessingFailure(
    [property: Key(0)] DateOnly ValueDate, [property: Key(1)] Guid GenerationId,
    [property: Key(2)] DateTimeOffset ObservedAtUtc, [property: Key(3)] string Component,
    [property: Key(4)] string Code, [property: Key(5)] string Detail);

/// <summary>Typed expected retry conditions; arbitrary exception messages never confer transient status.</summary>
public sealed class OptionFeedProcessingException(string code, string? detail = null) : Exception(detail is null ? code : code + ": " + detail)
{
    public string Code { get; } = code;
    public bool IsTransient => Code is "UnderlyingQuoteUnavailable" or "Recovering"
        or "OptionMonitoring.WORKER.NOT_ADMITTED" or "OptionMonitoring.SESSION.CHANGED";
}

/// <summary>Generation-scoped failure evidence independent of logs. Only verified recovery retires same-generation failures.</summary>
public sealed class OptionFeedProcessingHealth(TimeProvider? time = null)
{
    readonly TimeProvider clock = time ?? TimeProvider.System;
    readonly object gate = new();
    readonly Dictionary<(DateOnly Date, Guid Generation, string Component), Entry> entries = [];
    sealed record Entry(DateTimeOffset First, int Attempts, OptionFeedProcessingFailure? Failure);

    public void Report(DateOnly date, Guid generation, string component, Exception error, CancellationToken shutdown = default)
    {
        if (error is OperationCanceledException && shutdown.IsCancellationRequested) return;
        var now = clock.GetUtcNow();
        var key = (date, generation, component);
        lock (gate)
        {
            entries.TryGetValue(key, out var previous);
            var attempts = Math.Min(previous?.Attempts ?? 0, 3) + 1;
            var first = previous?.First ?? now;
            // Three attempts is a finite allowance, not a second recovery timer.
            var transient = error is OptionFeedProcessingException { IsTransient: true };
            var failure = previous?.Failure;
            if (!transient || attempts >= 3)
                failure ??= new(date, generation, now, component,
                    error is OptionFeedProcessingException typed ? typed.Code : error.GetType().Name, Redact(error.ToString()));
            if (entries.Count >= 4096 && !entries.ContainsKey(key))
            {
                // Bounded evidence replaces its oldest entry; overflow itself still forces recovery.
                key = (date, generation, "OptionProcessingHealth.Capacity");
                if (!entries.ContainsKey(key)) entries.Remove(entries.OrderBy(p => p.Value.First).First().Key);
                failure = new(date, generation, now, key.Item3, "CapacityExceeded", "Option processing failure evidence capacity exceeded.");
            }
            entries[key] = new(first, attempts, failure);
        }
    }

    public static string Redact(string text)
    {
        foreach (var name in new[] { "DATABENTO_API_KEY", "IFM_DATASET_WORKER_BOOTSTRAP" })
            if (Environment.GetEnvironmentVariable(name) is { Length: > 0 } secret) text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
        return text;
    }

    public void Succeeded(DateOnly date, Guid generation, string component)
    {
        lock (gate)
            if (entries.TryGetValue((date, generation, component), out var entry) && entry.Failure is null)
                entries.Remove((date, generation, component));
    }

    public OptionFeedProcessingFailure? Read(DateOnly date, Guid generation)
    {
        lock (gate) return entries.Where(p => p.Key.Date == date && (p.Key.Generation == generation || p.Key.Generation == Guid.Empty))
            .Select(p => p.Value.Failure).Where(f => f is not null).OrderBy(f => f!.ObservedAtUtc).FirstOrDefault();
    }

    public void Recovered(DateOnly date, Guid generation, DateTimeOffset through)
    {
        lock (gate)
            foreach (var key in entries.Where(p => p.Key.Date == date && (p.Key.Generation == generation || p.Key.Generation == Guid.Empty)
                && (p.Value.Failure?.ObservedAtUtc ?? p.Value.First) <= through).Select(p => p.Key).ToArray()) entries.Remove(key);
    }
}
