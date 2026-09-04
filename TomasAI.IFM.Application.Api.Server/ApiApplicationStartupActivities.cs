using System.Collections.Concurrent;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.FinancialModelingPrep;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Application.MarketData.MarketOutlook;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Model.Processing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// API-hosted adapters for the activities sequenced by the Application event actor. Lifecycle
/// mutations are submitted through typed actor APIs; this adapter never starts a native feed directly.
/// </summary>
public sealed class ApiApplicationStartupActivities(
    IFuturesMarketSessionAuthority marketSessionAuthority,
    IDatabentoContractAuthority contractAuthority,
    ICurrentFuturesContractCatalog contractCatalog,
    IFmpMarketDataImportCoordinator referenceImportCoordinator,
    IMarketDataFeedCommandApi marketDataFeedCommandApi,
    IMarketDataFeedQueryApi marketDataFeedQueryApi,
    IMarketDataAnalyticsCommandApi analyticsCommandApi,
    IHistoricalDataLoaderStore historicalDataLoaderStore,
    IDbContextFactory dbContextFactory,
    DatabentoMarketDataApi marketDataApi,
    IMarketOutlookUpdateWriter marketOutlookWriter,
    IMarketOutlookOperations marketOutlookOperations,
    IMarketOutlookHotCache marketOutlookCache,
    HistoricalAnalyticsWarmupOptions historicalWarmupOptions,
    ApplicationStartupOptions options,
    TimeProvider timeProvider,
    ILogger<ApiApplicationStartupActivities> logger) : IApplicationStartupActivities
{
    readonly ConcurrentDictionary<DateOnly, FuturesContractV3ReadModel[]> contractsByValueDate = new();

    public ValueTask<ApplicationStartupActivityOutcome> ResolveAuthorityAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = marketSessionAuthority.Current;
        if (!session.IsValid)
            throw new InvalidOperationException("The authoritative futures market session is invalid.");
        if (session.OperationalValueDate != context.ValueDate)
            throw new InvalidOperationException(
                $"Startup value date {context.ValueDate:yyyy-MM-dd} does not match authority {session.OperationalValueDate:yyyy-MM-dd}.");
        return ValueTask.FromResult(ApplicationStartupActivityOutcome.AlreadySatisfied);
    }

    public async ValueTask<ApplicationStartupActivityOutcome> ReconcileReferenceDataAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken)
    {
        var result = await referenceImportCoordinator.ImportAsync(
            new(context.ValueDate, context.ValueDate),
            cancellationToken).ConfigureAwait(false);
        return result.RejectedSubmissions == 0
            ? ApplicationStartupActivityOutcome.Started
            : ApplicationStartupActivityOutcome.Degraded;
    }

    public async ValueTask<ApplicationStartupActivityOutcome> ReconcileCurrentContractsAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken)
    {
        var assignments = await contractAuthority.ReconcileAsync(
            context.ValueDate, nameof(ApiApplicationStartupActivities), cancellationToken).ConfigureAwait(false);
        var selectedIds = assignments.Select(value => value.ContractId).ToHashSet(StringComparer.Ordinal);
        var es = await contractCatalog.GetByRootAsync("ES", cancellationToken).ConfigureAwait(false);
        var vx = await contractCatalog.GetByRootAsync("VX", cancellationToken).ConfigureAwait(false);
        var contracts = es.Concat(vx)
            .Where(contract => selectedIds.Contains(contract.ContractId))
            .Where(contract => !string.IsNullOrWhiteSpace(contract.ContractId))
            .DistinctBy(contract => contract.ContractId, StringComparer.Ordinal)
            .ToArray();
        if (contracts.Count(contract => StringComparer.OrdinalIgnoreCase.Equals(contract.Symbol, "ES")) != 1
            || contracts.Count(contract => StringComparer.OrdinalIgnoreCase.Equals(contract.Symbol, "VX")) != 2)
            throw new InvalidOperationException(
                "The current-contract set must contain exactly one quarterly ES plus current-month and next-month VX.");

        contractsByValueDate[context.ValueDate] = contracts;
        return ApplicationStartupActivityOutcome.Started;
    }

    public async ValueTask<ApplicationStartupActivityOutcome> StartMarketDataAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken)
    {
        if (marketSessionAuthority.Current.ActiveValueDate is null)
            return ApplicationStartupActivityOutcome.ScheduledStopped;

        var runtimeStatus = await marketDataFeedQueryApi.GetRuntimeStatusAsync().ConfigureAwait(false);
        if (!runtimeStatus.Success || runtimeStatus.Value is null || !runtimeStatus.Value.IsValid)
            throw new InvalidOperationException(
                $"Market Data runtime status could not be qualified ({runtimeStatus.ErrorCode}): {runtimeStatus.ErrorMessage}");

        var nativeUp = marketDataApi.IsDatabentoFeedUp();
        if (runtimeStatus.Value.IsRunning
            && runtimeStatus.Value.ActiveValueDate == context.ValueDate)
        {
            if (nativeUp)
                return ApplicationStartupActivityOutcome.AlreadySatisfied;
            throw new InvalidOperationException(
                "Market Data is marked running for the requested value date, but Databento is not up.");
        }

        if (!runtimeStatus.Value.IsRunning && nativeUp)
            throw new InvalidOperationException(
                "Databento is up while the application-owned Market Data runtime is marked stopped; refusing to create a duplicate generation.");

        if (runtimeStatus.Value.IsRunning
            && runtimeStatus.Value.ActiveValueDate is { } previousValueDate
            && previousValueDate != context.ValueDate)
        {
            var stopped = await marketDataFeedCommandApi.StopMarketDataFeedAsync(previousValueDate)
                .ConfigureAwait(false);
            if (!stopped.Success)
                throw new InvalidOperationException(
                    $"Previous Market Data value date {previousValueDate:yyyy-MM-dd} could not be fenced "
                    + $"({stopped.ErrorCode}): {stopped.ErrorMessage}");
            await WaitForMarketDataStateAsync(
                expectedRunning: false,
                expectedValueDate: null,
                "stop/fence",
                cancellationToken).ConfigureAwait(false);
        }

        if (!contractsByValueDate.TryGetValue(context.ValueDate, out var contracts))
            throw new InvalidOperationException("Qualified current contracts are unavailable.");

        var accepted = await marketDataFeedCommandApi.StartMarketDataFeedAsync(contracts, context.ValueDate)
            .ConfigureAwait(false);
        if (!accepted.Success)
            throw new InvalidOperationException(
                $"Market Data start command was rejected ({accepted.ErrorCode}): {accepted.ErrorMessage}");

        await WaitForMarketDataStateAsync(
            expectedRunning: true,
            expectedValueDate: context.ValueDate,
            "start",
            cancellationToken).ConfigureAwait(false);
        return ApplicationStartupActivityOutcome.Started;
    }

    public async ValueTask<ApplicationStartupActivityOutcome> WarmHistoricalAnalyticsAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var es = RequiredEsContract(context.ValueDate);
        var warmupRequired = HistoricalWarmupRequired();
        if (warmupRequired)
            await HydrateCurrentMarketOutlookAsync(
                    es.ContractId, context.ValueDate, context.CommandId, cancellationToken)
                .ConfigureAwait(false);
        var accepted = await analyticsCommandApi.EnsureHistoricalAnalyticsWarmupAsync(
            context.ValueDate,
            es.ContractId,
            context.ProcessBootId,
            context.CommandId).ConfigureAwait(false);
        if (!accepted.Success)
            throw new InvalidOperationException(
                $"Historical Analytics warm-up was rejected ({accepted.ErrorCode}): {accepted.ErrorMessage}");
        if (accepted.Value == Guid.Empty)
            throw new InvalidOperationException(
                "Historical Analytics warm-up was accepted without an attempt identity.");

        await WaitForHistoricalAnalyticsWarmupAsync(accepted.Value, cancellationToken)
            .ConfigureAwait(false);
        if (warmupRequired)
            await RequireWarmDailyMarketOutlookAsync(es.ContractId, context.ValueDate, cancellationToken)
                .ConfigureAwait(false);
        return ApplicationStartupActivityOutcome.Started;
    }

    async Task HydrateCurrentMarketOutlookAsync(
        string contractId,
        DateOnly valueDate,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContextFactory.MarketDataDb.GetMarketOutlookSnapshotAsync(
            contractId,
            valueDate,
            cancellationToken).ConfigureAwait(false);
        if (snapshot is null || snapshot.ValueDate != valueDate)
            return;

        var entityId = new MarketOutlookEntityId(contractId, valueDate);
        marketOutlookWriter.Submit(new HydrateMarketOutlookUpdate
        {
            UpdateId = Guid.NewGuid(),
            EntityId = entityId,
            ReceivedAtUtc = timeProvider.GetUtcNow().UtcDateTime,
            MarketDataAsOfUtc = snapshot.MarketDataAsOfUtc,
            CommandId = commandId,
            AggregateId = entityId.Format(),
            EventSource = nameof(ApiApplicationStartupActivities),
            Snapshot = snapshot
        });
    }

    public async ValueTask<ApplicationStartupActivityOutcome> StartRealtimeAnalyticsAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken)
    {
        if (marketSessionAuthority.Current.ActiveValueDate is null)
            return ApplicationStartupActivityOutcome.ScheduledStopped;

        var es = RequiredEsContract(context.ValueDate);
        var activations = FuturesIntradaySignalActivationProfile.Create(
            es.ContractId,
            context.ValueDate);
        foreach (var activation in activations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RequireAcceptedAsync(
                analyticsCommandApi.StartFuturesRsiSignalAsync(activation.Rsi),
                $"RSI {activation.TimeFrame}").ConfigureAwait(false);
            await RequireAcceptedAsync(
                analyticsCommandApi.StartFuturesAtrSignalAsync(activation.Atr),
                $"ATR {activation.TimeFrame}").ConfigureAwait(false);
            await RequireAcceptedAsync(
                analyticsCommandApi.StartFuturesAdxSignalAsync(activation.Adx),
                $"ADX {activation.TimeFrame}").ConfigureAwait(false);
            await RequireAcceptedAsync(
                analyticsCommandApi.StartFuturesMacdSignalAsync(activation.Macd),
                $"MACD {activation.TimeFrame}").ConfigureAwait(false);
        }

        await WaitForRealtimeAnalyticsAttachmentsAsync(activations, cancellationToken)
            .ConfigureAwait(false);
        return ApplicationStartupActivityOutcome.Started;
    }

    public ValueTask<ApplicationStartupActivityOutcome> QualifyOperationalStateAsync(
        ApplicationStartupContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!marketOutlookOperations.GetMetrics().IsProcessorReady)
            throw new InvalidOperationException("The local Market Outlook processor is not ready.");
        if (HistoricalWarmupRequired()
            && !HasWarmDailyMarketOutlook(
                RequiredEsContract(context.ValueDate).ContractId,
                context.ValueDate,
                out var warmupDetail))
        {
            // Historical warmup is an optional activity. Its failure already degrades the workflow;
            // final feed qualification must not promote that explicit degradation to Failed.
            logger.LogWarning(
                "Application operational qualification retained the historical analytics degradation. ValueDate={ValueDate}; Detail={Detail}.",
                context.ValueDate,
                warmupDetail);
        }
        if (marketSessionAuthority.Current.ActiveValueDate is not null
            && !marketDataApi.IsDatabentoFeedUp())
            throw new InvalidOperationException("Databento did not satisfy the bounded up/down qualification probe.");
        logger.LogInformation(
            "Application operational qualification passed for value date {ValueDate}.",
            context.ValueDate);
        return ValueTask.FromResult(ApplicationStartupActivityOutcome.AlreadySatisfied);
    }

    bool HistoricalWarmupRequired() =>
        historicalWarmupOptions.Enabled && historicalWarmupOptions.IsDevelopmentEnvironment;

    async Task RequireWarmDailyMarketOutlookAsync(
        string contractId,
        DateOnly valueDate,
        CancellationToken cancellationToken)
    {
        var drained = await marketOutlookOperations.WaitForIdleAsync(
            options.ParticipantTimeout, cancellationToken).ConfigureAwait(false);
        if (!drained)
            throw new TimeoutException(
                $"Market Outlook did not apply the historical warm-up within {options.ParticipantTimeout}.");
        RequireWarmDailyMarketOutlook(contractId, valueDate);
    }

    void RequireWarmDailyMarketOutlook(string contractId, DateOnly valueDate)
    {
        if (!HasWarmDailyMarketOutlook(contractId, valueDate, out var detail))
            throw new InvalidOperationException(detail);
    }

    bool HasWarmDailyMarketOutlook(string contractId, DateOnly valueDate, out string detail)
    {
        var entityId = new MarketOutlookEntityId(contractId, valueDate);
        if (!marketOutlookCache.TryGetCurrent(entityId, out var snapshot))
        {
            detail = $"Market Outlook has no snapshot after historical warm-up for {entityId.Format()}.";
            return false;
        }
        if (!snapshot.HasWarmDailyAnalytics
            || snapshot.FuturesEmaSignal is not { IsWarm: true }
            || snapshot.FuturesBbSignal is not { IsWarm: true })
        {
            detail = $"Market Outlook historical warm-up is incomplete for {entityId.Format()}: {snapshot.MissingInputs}";
            return false;
        }
        detail = string.Empty;
        return true;
    }

    FuturesContractV3ReadModel RequiredEsContract(DateOnly valueDate)
    {
        if (contractsByValueDate.TryGetValue(valueDate, out var contracts))
        {
            var es = contracts.FirstOrDefault(contract =>
                StringComparer.OrdinalIgnoreCase.Equals(contract.Symbol, "ES"));
            if (es is not null)
                return es;
        }
        throw new InvalidOperationException("The qualified current ES contract is unavailable.");
    }

    async Task WaitForMarketDataStateAsync(
        bool expectedRunning,
        DateOnly? expectedValueDate,
        string operation,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        while (timeProvider.GetElapsedTime(startedAt) < options.ParticipantTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var status = await marketDataFeedQueryApi.GetRuntimeStatusAsync().ConfigureAwait(false);
            if (status.Success
                && status.Value is { IsValid: true } current
                && current.IsRunning == expectedRunning
                && current.ActiveValueDate == expectedValueDate
                && marketDataApi.IsDatabentoFeedUp() == expectedRunning)
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"Market Data did not reach the qualified {operation} state within {options.ParticipantTimeout}.");
    }

    async Task WaitForHistoricalAnalyticsWarmupAsync(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetTimestamp();
        while (timeProvider.GetElapsedTime(startedAt) < options.ParticipantTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await historicalDataLoaderStore.GetAsync(attemptId, cancellationToken)
                .ConfigureAwait(false);
            if (state?.Status == HistoricalDataLoaderStatus.Completed)
                return;
            if (state?.Status == HistoricalDataLoaderStatus.Failed)
                throw new InvalidOperationException(
                    $"Historical Analytics warm-up {attemptId} failed: {state.ErrorMessage}");
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"Historical Analytics warm-up {attemptId} did not complete within {options.ParticipantTimeout}.");
    }

    async Task WaitForRealtimeAnalyticsAttachmentsAsync(
        IReadOnlyCollection<FuturesIntradaySignalActivation> activations,
        CancellationToken cancellationToken)
    {
        var expectedRsi = activations.Select(value => value.Rsi).ToHashSet();
        var expectedAtr = activations.Select(value => value.Atr).ToHashSet();
        var expectedAdx = activations.Select(value => value.Adx).ToHashSet();
        var expectedMacd = activations.Select(value => value.Macd).ToHashSet();
        var startedAt = timeProvider.GetTimestamp();

        while (timeProvider.GetElapsedTime(startedAt) < options.ParticipantTimeout)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (expectedRsi.IsSubsetOf(
                    FuturesTradeSessionBarAttachmentRegistry<FuturesRsiSignalEntityId>.Snapshot())
                && expectedAtr.IsSubsetOf(
                    FuturesTradeSessionBarAttachmentRegistry<FuturesAtrSignalEntityId>.Snapshot())
                && expectedAdx.IsSubsetOf(
                    FuturesTradeSessionBarAttachmentRegistry<FuturesAdxSignalEntityId>.Snapshot())
                && expectedMacd.IsSubsetOf(
                    FuturesTradeSessionBarAttachmentRegistry<FuturesMacdSignalEntityId>.Snapshot()))
                return;
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        throw new TimeoutException(
            $"Realtime Analytics consumers did not attach within {options.ParticipantTimeout}.");
    }

    static async Task RequireAcceptedAsync(
        Task<TomasAI.IFM.Shared.EventSourcing.ServiceResult<Guid>> operation,
        string activity)
    {
        var result = await operation.ConfigureAwait(false);
        if (!result.Success)
            throw new InvalidOperationException(
                $"{activity} start command was rejected ({result.ErrorCode}): {result.ErrorMessage}");
    }
}
