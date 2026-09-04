using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public sealed record DatabentoStage3Options
{
    public bool Enabled { get; init; }
    public TimeSpan LiveTradingPollInterval { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan LiveTradingEscalationWindow { get; init; } = TimeSpan.FromMinutes(5);
    public int LiveTradingMaximumCooperativeAttempts { get; init; } = 5;
    public TimeSpan LiveTradingHealthyQualificationPeriod { get; init; } = TimeSpan.FromMinutes(1);
    public TimeSpan OffTradingPollInterval { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan OffTradingStallTimeout { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan WorkerHandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan WorkerStartTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan WorkerCommandTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan WorkerGracefulStopTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan WorkerForceKillTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan WorkerQualificationTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public int MaximumProcessReplacementsPerIncident { get; init; } = 3;
    public TimeSpan ProcessReplacementWindow { get; init; } = TimeSpan.FromMinutes(15);
    public int ControlFrameMaximumBytes { get; init; } = 256 * 1024;

    public DatabentoStage3Options Validate()
    {
        if (LiveTradingPollInterval <= TimeSpan.Zero
            || LiveTradingEscalationWindow < LiveTradingPollInterval
            || LiveTradingMaximumCooperativeAttempts is < 1 or > 10
            || LiveTradingHealthyQualificationPeriod < LiveTradingPollInterval
            || OffTradingPollInterval <= TimeSpan.Zero
            || OffTradingStallTimeout < OffTradingPollInterval * 2
            || WorkerHandshakeTimeout <= TimeSpan.Zero
            || WorkerStartTimeout <= TimeSpan.Zero
            || WorkerCommandTimeout <= TimeSpan.Zero
            || WorkerGracefulStopTimeout <= TimeSpan.Zero
            || WorkerForceKillTimeout <= TimeSpan.Zero
            || WorkerQualificationTimeout <= TimeSpan.Zero
            || MaximumProcessReplacementsPerIncident is < 1 or > 10
            || ProcessReplacementWindow <= TimeSpan.FromMinutes(2)
            || ControlFrameMaximumBytes is < 4096 or > 256 * 1024)
            throw new InvalidOperationException("Databento Stage 3 resiliency options are invalid.");
        return this;
    }

    public TimeSpan ScheduledInterval(FuturesMarketState state) => state switch
    {
        FuturesMarketState.LiveTrading => LiveTradingPollInterval,
        FuturesMarketState.OffTrading => OffTradingPollInterval,
        _ => Timeout.InfiniteTimeSpan
    };
}

public enum DatasetRecoveryAction
{
    None = 0,
    CooperativeReset = 1,
    ReplaceProcess = 2,
    StopForClosure = 3
}

public sealed record DatasetIncidentSnapshot
{
    public required string Dataset { get; init; }
    public required DateOnly ValueDate { get; init; }
    public Guid IncidentId { get; init; }
    public Guid GenerationId { get; init; }
    public bool IsOpen { get; init; }
    public bool ProcessReplacementLatched { get; init; }
    public int CooperativeAttempts { get; init; }
    public int ProcessReplacements { get; init; }
    public TimeSpan UnhealthyDuration { get; init; }
    public TimeSpan HealthyDuration { get; init; }
    public DatabentoDatasetFailureReason FailureReason { get; init; }
    public DatasetRecoveryAction LastAction { get; init; }
    public DateTime ObservedOnUtc { get; init; }
}

public sealed record DatasetIncidentTransition(
    Guid TransitionId,
    Guid CorrelationId,
    DatasetIncidentSnapshot Snapshot,
    long RowVersion = 0);

public readonly record struct DatasetIncidentDecision(
    DatasetRecoveryAction Action,
    DatasetIncidentSnapshot Snapshot);

public interface IDatabentoDatasetProcessRecovery
{
    Task<DatabentoDatasetResetResult> ReplaceProcessAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken);
}

public sealed class UnavailableDatabentoDatasetProcessRecovery : IDatabentoDatasetProcessRecovery
{
    public Task<DatabentoDatasetResetResult> ReplaceProcessAsync(
        DatabentoDatasetResetRequest request,
        CancellationToken cancellationToken) => Task.FromResult(new DatabentoDatasetResetResult(
            request.Dataset,
            request.ExpectedGenerationId,
            Guid.Empty,
            false,
            "No supervised dataset-process recovery implementation is registered."));
}
