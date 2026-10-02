namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Terminal outcome of one hard-and-soft recovery episode.</summary>
public enum DatabentoRecoveryRequestOutcome
{
    AlreadyInProgress = 0,
    FullyHealthy = 1,
    DatabentoHealthyDownstreamDegraded = 2,
    Unrecoverable = 3,
    ApplicationStopping = 4
}

/// <summary>Typed outcome of an accepted recovery or an ignored concurrent request.</summary>
public sealed record DatabentoRecoveryRequestResult(
    Guid CorrelationId,
    DatabentoRecoveryRequestOutcome Outcome,
    DatabentoHardRecoveryResult? HardResult,
    string Detail);

/// <summary>Signals a completed episode that did not fully restore downstream service.</summary>
public sealed class DatabentoRecoveryEpisodeStatusException : Exception
{
    public DatabentoRecoveryRequestResult Result { get; }

    public DatabentoRecoveryEpisodeStatusException(DatabentoRecoveryRequestResult result)
        : base($"Recovery episode {result.CorrelationId} ended {result.Outcome}: {result.Detail}")
        => Result = result;
}

/// <summary>
/// Routes a hard reset to the single authoritative hard-and-soft recovery episode.
/// </summary>
public interface IDatabentoRecoveryRequester
{
    /// <summary>
    /// Sequentially executes the required recovery actions. Concurrent requests are ignored.
    /// A required action failure is logged and requests API shutdown before failure is returned.
    /// </summary>
    Task<DatabentoRecoveryRequestResult> HardResetRecoveryAsync(
        DatabentoHardRecoveryRequest request, CancellationToken cancellationToken = default);
}
