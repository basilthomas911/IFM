using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.MarketData.TradeStrategySymbols;

/// <summary>Two small ReferenceDb reads; no provider download, full-table scan or ID allocation on the UI path.</summary>
public sealed class StoredInstrumentDefinitionSymbolCatalog(IInstrumentDefinitionStore store) : ITradeStrategySymbolCatalog
{
    public async Task<ServiceResult<TradeStrategySymbolReadModel[]>> GetAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (family is not (TradeStrategyFamilyType.Futures or TradeStrategyFamilyType.FuturesOption))
            return new ServiceFailed<TradeStrategySymbolReadModel[]>(400, $"Trade strategy symbols are not supported for {family}.");
        try
        {
            var snapshot = await store.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is null) return new ServiceFailed<TradeStrategySymbolReadModel[]>(503, "Instrument definitions have not been loaded. Run the instrument-definition refresh.");
            var rows = await store.GetSymbolsAsync(snapshot.Id, family, cancellationToken).ConfigureAwait(false);
            if (rows.Any(x => x.Validate().Count != 0) || rows.Select(x => x.Id).Distinct().Count() != rows.Length)
                throw new InvalidOperationException("Invalid instrument-definition product index.");
            return new ServiceOk<TradeStrategySymbolReadModel[]>(rows);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new ServiceFailed<TradeStrategySymbolReadModel[]>(503, $"Instrument definition lookup failed: {ex.Message}"); }
    }
}
