using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Remote;

public sealed partial class MarketDataReadMirror
{
    readonly object gate = new();
    readonly DatabentoContractRegistrationRegistry registry = new([], new()
        { FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3"), Contracts = [] });
    readonly DatasetWorkerCurrentValues values = new();
    MarketDataBackendStatus status = new(false, null, []);
    Guid optionGeneration;
    DateTime? optionFailureUtc;
    Guid failedOptionGeneration;
    readonly Dictionary<string, DatasetWorkerAdmission> identities = new(StringComparer.Ordinal);
    Guid hostInstance;
    long revision;
    readonly HashSet<Guid> retiredHosts = [];
    public bool Apply(MarketDataReadSnapshot snapshot)
    {
        lock (gate)
        {
            if (snapshot.Status.HostInstanceId != Guid.Empty)
            {
                if (retiredHosts.Contains(snapshot.Status.HostInstanceId) || hostInstance == snapshot.Status.HostInstanceId && snapshot.Status.Revision < revision) return false;
                if (hostInstance != Guid.Empty && hostInstance != snapshot.Status.HostInstanceId) retiredHosts.Add(hostInstance);
                hostInstance = snapshot.Status.HostInstanceId; revision = snapshot.Status.Revision;
            }
            status = snapshot.Status;
            var failure = status.Datasets.FirstOrDefault(x => x.Role == "Options" && x.Lifecycle is "StoppedError" or "CleanupUnconfirmed");
            if (failure is null) { optionFailureUtc = null; failedOptionGeneration = Guid.Empty; }
            else if (failedOptionGeneration != failure.GenerationId) { failedOptionGeneration = failure.GenerationId; optionFailureUtc = failure.ChangedAtUtc == default ? DateTime.UtcNow : failure.ChangedAtUtc.UtcDateTime; }
            foreach (var root in snapshot.Contracts.GroupBy(contract => contract.Symbol, StringComparer.OrdinalIgnoreCase))
                registry.ReplaceFuturesRolloverSet(root.Key, root.ToArray());
            foreach (var pair in identities.ToArray())
                if (!snapshot.Status.Datasets.Any(state => state.Role == "Futures" && state.Dataset == pair.Key
                    && state.WorkerInstanceId == pair.Value.WorkerInstanceId && state.GenerationId == pair.Value.GenerationId && state.Lifecycle == "Healthy"))
                { values.ClearDataset(pair.Key); identities.Remove(pair.Key); }
            foreach (var manifest in snapshot.Manifests)
            {
                var state = snapshot.Status.Datasets.SingleOrDefault(state => state.Role == "Futures" && state.Dataset == manifest.Dataset && state.Lifecycle == "Healthy");
                if (state is null) continue;
                var admission = new DatasetWorkerAdmission(state.Dataset, manifest.ValueDate, state.WorkerInstanceId, state.GenerationId, manifest.Revision);
                values.ActivateDataset(admission, manifest.GetRegistrations());
                values.SetDatasetHealth(admission, true);
                identities[state.Dataset] = admission;
            }
            foreach (var frame in snapshot.CurrentValues) values.AcceptPublication(frame);
            if (snapshot.Status.ValueDate is { } activeDate)
                foreach (var seed in snapshot.BootstrapPrices ?? []) values.SeedBootstrapPrice(activeDate, seed.Price, seed.Source);
            var option = snapshot.Status.Datasets.SingleOrDefault(x => x.Role == "Options" && x.Lifecycle == "Healthy");
            if (optionChainCache is OptionChainCache.OptionChainCache cache)
            {
                if (option is null) { cache.Fence(optionGeneration); optionGeneration = Guid.Empty; }
                else { optionGeneration = option.GenerationId; cache.Admit(optionGeneration); foreach (var item in snapshot.OptionChains ?? []) AcceptOption(item); }
            }
            if (!snapshot.Status.Datasets.Any(state => state.Role == "Futures" && state.Running)) values.Stop();
            return true;
        }
    }
    public bool Accept(DatasetPublicationEnvelope envelope)
    { lock (gate) return values.AcceptPublication(envelope); }
    public bool NeedsOption(Guid generation, string scope, long version)
    { lock (gate) return generation == optionGeneration && optionChainCache.GetStatus(new(scope, generation)).ChainVersion < version; }
    public void AcceptOption(OptionChainCache.OptionChainPublication publication)
    {
        lock (gate)
            if (publication.Snapshot.GenerationId == optionGeneration && optionChainCache is OptionChainCache.OptionChainCache cache)
                cache.Publish(publication.Snapshot, publication.ValueDate, publication.ConfigurationDigest, publication.Version, publication.LookupScope);
    }
    public void Disconnect()
    {
        lock (gate)
        {
            foreach (var identity in identities.Values) values.ClearDataset(identity.Dataset);
            identities.Clear();
            if (optionChainCache is OptionChainCache.OptionChainCache cache) cache.Fence(optionGeneration);
            optionGeneration = Guid.Empty;
            status = new(false, null, []);
        }
    }
    static bool MissingInstrument(out uint id) { id = 0; return false; }
    static bool MissingOption(out OptionTickerPriceSnapshot price) { price = default; return false; }
    MarketDataFeedRuntimeStatusReadModel RuntimeStatus()
    {
        lock (gate)
        {
            var current = values.GetStatus();
            var failure = status.Datasets.FirstOrDefault(x => x.Role == "Options" && x.Lifecycle is "StoppedError" or "CleanupUnconfirmed");
            return new() { IsRunning = current.IsRunning, ActiveValueDate = current.ActiveValueDate, ObservedAtUtc = DateTimeOffset.UtcNow,
                OptionFeedError = failure?.Error, OptionCleanupConfirmed = failure?.Lifecycle == "StoppedError",
                OptionWorkerInstanceId = failure?.WorkerInstanceId ?? Guid.Empty, OptionGenerationId = failure?.GenerationId ?? Guid.Empty,
                OptionDataset = failure?.Dataset, OptionFailureUtc = optionFailureUtc };
        }
    }
}
