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
