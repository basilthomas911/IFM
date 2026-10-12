namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
public enum DatabentoHardRecoveryOutcome
{
    DatabentoHealthy = 1,
    Unrecoverable = 2,
    ApplicationStopping = 3
}
