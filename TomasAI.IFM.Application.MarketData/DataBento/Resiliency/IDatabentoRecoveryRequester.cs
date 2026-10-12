namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

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
