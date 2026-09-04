using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Framework.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Worker;

/// <summary>A real synthetic native/managed dataset pipeline owned entirely by the worker process.</summary>
internal sealed class DatasetWorkerRuntime : IAsyncDisposable
{
    readonly IDatabentoMarketDataEpoch epoch;

    DatasetWorkerRuntime(IDatabentoMarketDataEpoch epoch) => this.epoch = epoch;

    public Guid GenerationId => epoch.GetHealth().DatasetFeedStatuses?.Single().GenerationId
        ?? Guid.Empty;
    public bool IsHealthy => epoch.IsFeedUp(TimeSpan.FromSeconds(1));
    public string Detail
    {
        get
        {
            var health = epoch.GetHealth();
            return $"running={health.Running}; aggregation={health.AggregationRunning}; "
                + $"contracts={health.ConfiguredContracts}; trades={health.SourceTradeRecords}; "
                + $"quotes={health.SourceQuoteRecords}; failures={health.ProcessingFailures + health.PublicationFailures}";
        }
    }

    public static async Task<DatasetWorkerRuntime> StartAsync(
        string dataset,
        IReadOnlyList<string> contractIds,
        DateOnly valueDate,
        FeedDeploymentProfile deploymentProfile,
        FeedDataSourceMode dataSource,
        SyntheticFeedOptions synthetic,
        ITickAggregationEventPublisher publisher,
        CancellationToken cancellationToken)
    {
        if (contractIds.Count == 0 || contractIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one synthetic contract is required.", nameof(contractIds));
        var feedOptions = DatabentoFeedOptions.ForProfile(deploymentProfile, dataset) with
        {
            DataSource = dataSource,
            Synthetic = synthetic
        };
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = feedOptions,
            Contracts = contractIds.Select((contractId, index) => new DatabentoContractRegistration
            {
                DomainContractId = contractId,
                ProviderContractName = contractId,
                AssetTypeId = AssetTypeId.Futures,
                RootSymbol = Root(contractId),
                Dataset = dataset,
                OnTheRun = index == 0,
                Rollover = true
            }).ToArray()
        };
        var factory = new DatabentoMarketDataEpochFactory(
            new DatabentoFeedFactory(), publisher, options);
        var epoch = factory.Create(valueDate);
        try
        {
            await epoch.StartAsync(cancellationToken).ConfigureAwait(false);
            return new(epoch);
        }
        catch
        {
            await epoch.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    static string Root(string contractId)
    {
        var root = new string(contractId.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        return root.Length == 0
            ? throw new ArgumentException("Synthetic contract ID must start with a root symbol.", nameof(contractId))
            : root;
    }

    public async Task<Guid> ResetAsync(CancellationToken cancellationToken)
    {
        var health = epoch.GetHealth().DatasetFeedStatuses?.Single()
            ?? throw new InvalidOperationException("Synthetic worker dataset is not active.");
        var result = await epoch.ResetDatasetAsync(new DatabentoDatasetResetRequest(
            health.Dataset,
            health.GenerationId,
            epoch.ValueDate,
            DatabentoDatasetFailureReason.NativeDrainStalled,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            Guid.NewGuid()), cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Detail);
        return result.GenerationId;
    }

    public async ValueTask DisposeAsync() => await epoch.DisposeAsync().ConfigureAwait(false);

}
