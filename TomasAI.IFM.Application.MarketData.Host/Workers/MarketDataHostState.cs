using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Host-owned admitted generations and current futures values; options have their own admission lane.</summary>
public sealed class MarketDataHostState(DatasetWorkerCurrentValues futures, DatasetWorkerAdmissionRegistry options)
{
    public Func<DateOnly, string[]>? EnabledTrades { get; init; }
    public TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth? ProcessingHealth { get; init; }
    public Func<CancellationToken, Task>? RetireOptions { get; init; }
    public Func<IReadOnlyList<TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainRequest>, CancellationToken, Task>? AdoptOptions { get; init; }
    public Func<TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainRequest, CancellationToken, Task<TomasAI.IFM.Application.MarketData.Pricing.WorkerOptionChainRequest>>? RequalifyOptions { get; init; }
    readonly object gate = new();
    readonly Dictionary<(string Dataset, DatasetPublicationKind Kind, string Contract), DatasetPublicationEnvelope> latest = [];
    public DatasetWorkerCurrentValues Futures { get; } = futures;
    public DatasetWorkerAdmissionRegistry Options { get; } = options;
    public void Accept(DatasetPublicationEnvelope envelope)
    {
        lock (gate)
        {
            if (!Futures.AcceptPublication(envelope)) return;
            var contract = envelope.Kind switch
            {
                DatasetPublicationKind.MarketPrice => MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events.FuturesMarketPriceUpdatedRealtimeEvent>(envelope.Payload).Price.ContractId,
                DatasetPublicationKind.SessionStatistics => MessagePack.MessagePackSerializer.Deserialize<TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.FuturesSessionStatisticsUpdatedRealtimeEvent>(envelope.Payload).Statistics.ContractId,
                _ => null
            };
            if (contract is not null) latest[(envelope.Dataset, envelope.Kind, contract)] = envelope;
        }
    }
    public DatasetPublicationEnvelope[] Capture()
    { lock (gate) return latest.Values.OrderBy(frame => frame.PublicationSequence).ToArray(); }
    public void Activate(string role, DatasetSubscriptionManifest manifest, DatasetWorkerProcessSnapshot process)
    {
        var admission = new DatasetWorkerAdmission(manifest.Dataset, manifest.ValueDate, process.WorkerInstanceId,
            process.GenerationId, process.ManifestRevision);
        if (role == "Futures") { Futures.ActivateDataset(admission, manifest.GetRegistrations()); Futures.SetDatasetHealth(admission, true); }
        else
        {
            ProcessingHealth?.Recovered(manifest.ValueDate, Guid.Empty, DateTimeOffset.UtcNow);
            Options.Admit(admission);
        }
    }
    public void Fence(string role, string dataset, Guid generation)
    {
        if (role == "Futures")
        {
            lock (gate)
            {
                Futures.ClearDataset(dataset);
                foreach (var key in latest.Keys.Where(key => key.Dataset == dataset).ToArray()) latest.Remove(key);
            }
        }
        else Options.Close(dataset, generation);
    }
}
