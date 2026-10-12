using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using static TomasAI.IFM.Application.Storage.PortfolioDb.PortfolioDbFinancialSupport;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Persists canonical Portfolio ledger configuration operations.</summary>
public interface ILedgerConfigurationStore
{
    /// <summary>Configures a canonical Portfolio ledger operation atomically.</summary>
    Task<LedgerConfigurationCompletedEvent> ConfigureAsync(ConfigureLedgerCommand request,
        Func<LedgerConfigurationReceipt, LedgerConfigurationCompletedEvent> complete,
        Func<LedgerReconciliationResult, string> hash, CancellationToken token = default);
}
