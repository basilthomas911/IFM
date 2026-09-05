using TomasAI.IFM.UI.Net.Models.MarketData;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.ViewModels.App;

/// <summary>Single-flight read-only central operations dashboard, independent of UI quote consumers.</summary>
public sealed class MarketDataOperationsHealthViewModel : ObservableObject, IAsyncDisposable
{
    readonly IMarketDataOperationsHealthQueryService service;
    readonly AsyncOperation refresh;
    MarketDataOperationsHealthSnapshot? snapshot;
    string failureReason = "Operations health has not been queried.";

    public MarketDataOperationsHealthViewModel(IMarketDataOperationsHealthQueryService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        refresh = new AsyncOperation(RefreshCoreAsync);
    }

    public IAsyncOperation RefreshOperation => refresh;
    public MarketDataOperationsHealthSnapshot? Snapshot => snapshot;
    public string Status => snapshot?.OverallStatus ?? "Unavailable";
    public string Summary => snapshot is null ? $"Operations health: unavailable. {failureReason}"
        : $"Operations health: {snapshot.OverallStatus} | Session: {snapshot.SessionState} | "
          + $"Value date: {snapshot.ValueDate?.ToString("yyyy-MM-dd") ?? "none"} | Revision: {snapshot.Revision}";
    public string Observation => snapshot is null ? "No current central observation. Previous green values are not retained."
        : $"Central observation: {Utc(snapshot.ObservedOnUtc)} | Last probe: {Utc(snapshot.LastProbeUtc)} | "
          + $"Next probe: {Utc(snapshot.NextProbeUtc)} | Stale-generation publications rejected: {snapshot.RejectedStaleGenerationPublications}";
    public IReadOnlyList<MarketDataOperationsStageRow> Stages => snapshot?.Stages.Select(value => new MarketDataOperationsStageRow(
        value.Stage, value.Status, value.Required ? "Required" : "Optional", value.Pending, value.Capacity,
        value.HighWater, value.Received, value.Completed, value.Failed, value.Coalesced, value.Saturated,
        Age(snapshot.ObservedOnUtc, value.MarketDataAsOfUtc), Utc(value.MarketDataAsOfUtc),
        Utc(value.LastSucceededUtc), Duration(value.OldestPendingAge), Duration(value.P50Latency),
        Duration(value.P95Latency), Duration(value.P99Latency), value.ReasonCode, value.Reason)).ToArray() ?? [];
    public IReadOnlyList<MarketDataOperationsDatasetRow> Datasets => snapshot?.Datasets.Select(value => new MarketDataOperationsDatasetRow(
        value.Dataset, value.Status, value.SessionState, value.ProcessId, value.GenerationId.ToString("D"),
        value.Running, value.Healthy, $"{value.RecordsConsumed}/{value.RecordsProduced}",
        $"{value.RingUsed}/{value.RingCapacity}", $"{value.ChannelBatchCount}/{value.ChannelBatchCapacity}",
        $"{value.RecordsCompleted}/{value.RecordsStarted}", value.CooperativeAttempts, value.ProcessReplacementCount,
        value.ProcessReplacementLatched, Duration(value.IncidentAge), Utc(value.LastHealthyUtc),
        Utc(value.NextProbeUtc), value.Reason, value.WorkerInstanceId.ToString("D"),
        Utc(value.StartedOnUtc), value.GracefulStopSucceeded, value.ForcedTermination)).ToArray() ?? [];

    public Task RefreshAsync(CancellationToken cancellationToken = default) => refresh.ExecuteAsync(cancellationToken);
    public void Cancel() => refresh.Cancel();

    async Task RefreshCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetAsync(cancellationToken);
            snapshot = result.IsSuccess ? result.Value : null;
            failureReason = result.Error?.Message ?? "Operations health returned no snapshot.";
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            snapshot = null;
            failureReason = "Operations health could not be read; current health is unknown.";
        }
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Observation));
        OnPropertyChanged(nameof(Stages));
        OnPropertyChanged(nameof(Datasets));
    }

    static string Age(DateTime observed, DateTime? source) => source is null ? "Unknown"
        : source > observed ? "Clock ahead" : Duration(observed - source.Value);
    static string Utc(DateTime? value) => value?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'") ?? "Unknown";
    static string Duration(TimeSpan? value) => value is null ? "Unknown"
        : value.Value.TotalSeconds >= 60 ? $"{value.Value.TotalMinutes:F1} min"
        : value.Value.TotalSeconds >= 1 ? $"{value.Value.TotalSeconds:F1} s" : $"{value.Value.TotalMilliseconds:F1} ms";
    public ValueTask DisposeAsync() => refresh.DisposeAsync();
}

public sealed record MarketDataOperationsStageRow(string Stage, string Status, string Requirement,
    long Pending, long Capacity, long HighWater, long Received, long Completed, long Failed, long Coalesced,
    long Saturated, string SourceAge, string MarketDataAsOfUtc, string LastSucceededUtc, string OldestPending,
    string P50Latency, string P95Latency, string P99Latency, string ReasonCode, string Reason);

public sealed record MarketDataOperationsDatasetRow(string Dataset, string Status, string Session,
    int ProcessId, string Generation, bool Running, bool Healthy, string NativeConsumedProduced,
    string RingUsedCapacity, string ChannelUsedCapacity, string AggregationCompletedStarted,
    int CooperativeAttempts, int ProcessReplacements, bool ReplacementLatched, string IncidentAge,
    string LastHealthyUtc, string NextProbeUtc, string Reason, string WorkerInstance,
    string StartedOnUtc, bool GracefulStopSucceeded, bool ForcedTermination);
