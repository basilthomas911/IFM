using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.TradeStrategySymbols;

/// <summary>Symbol requests only read ReferenceDb; they never contact Databento.</summary>
public sealed class StoredInstrumentDefinitionSymbolSource(IInstrumentDefinitionStore store) : ITradeStrategySymbolSource
{
    public async Task<IReadOnlyList<TradeStrategyProduct>> DiscoverAsync(TradeStrategyFamilyType family, CancellationToken cancellationToken)
    {
        var snapshot = await store.GetSnapshotAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Instrument definitions have not been loaded. Run the instrument-definition refresh.");
        return await store.GetProductsAsync(snapshot.Id, family, cancellationToken).ConfigureAwait(false);
    }
}
