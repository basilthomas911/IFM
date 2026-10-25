using TomasAI.IFM.Application.Storage;


using TomasAI.IFM.Application.MarketData.Contracts;


using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Application.Identity;


namespace TomasAI.IFM.Domain.Reference.Repository;

public sealed class TradeStrategySymbolStore(IDbContextFactory db, ISequenceIdApi sequenceIds) : ITradeStrategySymbolStore
{
    public async Task<TradeStrategySymbolReadModel> GetOrCreateAsync(TradeStrategyProduct product, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(product);
        product.Validate();
        var existing = await FindProduct(product, cancellationToken).ConfigureAwait(false);
        if (existing is not null) return existing;
        var id = checked((int)await sequenceIds.GetSequenceIdAsync(SequenceName.Reference_TradeStrategySymbolId, cancellationToken).ConfigureAwait(false));
        if (id <= 0) throw new InvalidOperationException("The product sequence returned a non-positive identity.");
        await db.ReferenceDb.InsertTradeStrategyProductAsync(product,id,cancellationToken).ConfigureAwait(false);
        return await FindProduct(product, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The product identity insert could not be read back.");
    }
    Task<TradeStrategySymbolReadModel?> FindProduct(TradeStrategyProduct product, CancellationToken cancellationToken) =>
        db.ReferenceDb.GetTradeStrategyProductAsync(product,cancellationToken);
    public Task<TradeStrategySymbolReadModel?> FindAsync(TradeStrategyFamilyType family,int id,CancellationToken cancellationToken) =>
        db.ReferenceDb.GetTradeStrategySymbolAsync(family,id,cancellationToken);
}
