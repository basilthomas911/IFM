


using TomasAI.IFM.Domain.Portfolio.Shared.Financial;



namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Persists canonical Portfolio ledger configuration operations.</summary>
public interface ILedgerConfigurationStore
{
    /// <summary>Configures a canonical Portfolio ledger operation atomically.</summary>
    Task<LedgerConfigurationCompletedEvent> ConfigureAsync(ConfigureLedgerCommand request,
        Func<LedgerConfigurationReceipt, LedgerConfigurationCompletedEvent> complete,
        Func<LedgerReconciliationResult, string> hash, CancellationToken token = default);
}
