using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.OperationsHealth;

public sealed partial class MarketDataOperationsHealthService
{
    // These stages belong to the managed supervisor/ring-buffer pipeline. The Rust
    // host publishes dataset ownership instead; enum IDs remain stable for old journals.
    static bool IsLegacyWorkerStage(MarketDataOperationStage stage) => stage is
        MarketDataOperationStage.DatabentoNative or MarketDataOperationStage.DatabentoInterop or
        MarketDataOperationStage.DatabentoAggregation or MarketDataOperationStage.DatabentoLifecycle or
        MarketDataOperationStage.DatabentoRefresh or MarketDataOperationStage.DatabentoWorkerProcess or
        MarketDataOperationStage.DatabentoRealtimePublication or MarketDataOperationStage.UiDelivery;

    /// <summary>Copies native dataset ownership without inventing process, ring or aggregation counters.</summary>
    static MarketDataDatasetHealthReadModel[] ProjectNativeDatasets(MarketDataBackendStatus backend,
        MarketSessionReadModel session, DateTime observed, IReadOnlyList<MarketDataDatasetHealthReadModel> previous) =>
        backend.Datasets.Select(dataset =>
        {
            // Role is part of the identity: futures and options share GLBX.MDP3.
            var identity = dataset.Role + "/" + dataset.Dataset;
            var prior = previous.FirstOrDefault(row => string.Equals(row.Dataset, identity, StringComparison.Ordinal)
                && row.GenerationId == dataset.GenerationId);
            var status = dataset.Lifecycle switch
            {
                "Failed" or "StoppedError" or "Down" => "Red",
                "Starting" or "Recovering" or "Resetting" or "Reconfiguring" => "Orange",
                "Idle" or "Inactive" or "Stopped" when !dataset.Running => "Inactive",
                _ when !dataset.Running || !dataset.Healthy => "Red",
                _ => "Green"
            };
            return new MarketDataDatasetHealthReadModel
            {
                ResourceKind = "RustDataset",
                Counters = dataset.Counters,
                Dataset = identity,
                Status = status,
                Reason = BoundReason("Rust dataset resource: " + dataset.Lifecycle
                    + (string.IsNullOrWhiteSpace(dataset.Error) ? "." : ". " + dataset.Error)),
                SessionState = session.State.ToString(),
                ValueDate = dataset.ValueDate ?? backend.ValueDate,
                ProcessId = dataset.ProcessId,
                WorkerInstanceId = dataset.WorkerInstanceId,
                GenerationId = dataset.GenerationId,
                Running = dataset.Running,
                Healthy = status == "Green",
                LastObservedUtc = observed,
                LastHealthyUtc = status == "Green" ? observed : prior?.LastHealthyUtc
            };
        }).ToArray();
}
