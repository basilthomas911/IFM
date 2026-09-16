using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query.Model;

/// <summary>Maps Databento lifecycle data into public query read models.</summary>
internal static class DatabentoQueryModel
{
    /// <summary>Maps the current lifecycle snapshot.</summary>
    internal static DatabentoReadinessReadModel MapReadiness(DatabentoLifecycleSnapshot value) => new()
    {
        State = value.State.ToString(), DisplayHealth = value.LastObservation?.DisplayHealth.ToString() ?? "Inactive",
        CoreReady = value.CoreReady, ValueDate = value.ValueDate, CorrelationId = value.CorrelationId,
        NativeGeneration = value.NativeGeneration, RecoveryAttempt = value.RecoveryAttempt, Reason = value.Reason,
        ChangedOnUtc = value.ChangedOnUtc, NextRetryOnUtc = value.NextRetryOnUtc,
        Feeds = [.. (value.LastObservation?.FeedStatusDetails ?? []).Select(MapFeed)]
    };

    /// <summary>Maps a current contract assignment.</summary>
    internal static DatabentoContractAssignmentReadModel MapAssignment(FuturesRolloverContractAssignment value) => new()
    {
        Role = value.ContractRole.ToString(), RootSymbol = value.RootSymbol, ContractId = value.ContractId,
        LastTradeDate = value.LastTradeDate, NextRolloverDate = value.NextRolloverDate,
        RowVersion = value.RowVersion, UpdatedOnUtc = value.UpdatedOnUtc
    };

    /// <summary>Maps a watchdog observation.</summary>
    internal static DatabentoWatchdogObservationReadModel MapObservation(DatabentoWatchdogObservation value) => new()
    {
        Id = value.WatchdogStatusLogId, ObservationId = value.ObservationId, CorrelationId = value.CorrelationId,
        ValueDate = value.ValueDate, ObservedOnUtc = value.ObservedOnUtc, OperationReason = value.OperationReason.ToString(),
        MajorStatus = value.MajorStatus.ToString(), DisplayHealth = value.DisplayHealth.ToString(),
        CoreReady = value.CoreContractsReady, RecoveryAttempt = value.RecoveryAttempt, FailureStage = value.FailureStage,
        FailureDetail = value.FailureDetail, Feeds = [.. value.FeedStatusDetails.Select(MapFeed)], RowVersion = value.RowVersion
    };

    /// <summary>Maps one feed status observation.</summary>
    internal static DatabentoFeedStatusReadModel MapFeed(DatabentoFeedWatchdogStatus value) => new()
    {
        FeedInstanceId = value.FeedInstanceId, Dataset = value.Dataset, FeedKind = value.FeedKind,
        Criticality = value.Criticality.ToString(), MajorStatus = value.MajorStatus.ToString(), NativeState = value.NativeState,
        ProducerAlive = value.ProducerAlive, AggregationWorkerRunning = value.AggregationWorkerRunning,
        ExpectedSubscriptions = value.ExpectedSubscriptions, ReceivedSubscriptions = value.ReceivedSubscriptions,
        ProviderMessageCount = value.ProviderMessageCount,
        LastProviderMessageAgeTicks = value.LastProviderMessageAge == TimeSpan.MaxValue ? long.MaxValue : value.LastProviderMessageAge.Ticks,
        RingUsed = value.RingUsed, RingCapacity = value.RingCapacity, FailureDetail = value.FailureDetail,
        ContractIds = [.. value.ContractIds]
    };
}