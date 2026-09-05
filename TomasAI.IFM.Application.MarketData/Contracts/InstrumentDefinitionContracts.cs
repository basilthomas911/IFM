using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public sealed record InstrumentDefinitionSnapshot(Guid Id, DateTime CompletedUtc, long RecordCount, string[] Datasets);

public interface IInstrumentDefinitionStore
{
    Task<InstrumentDefinitionSnapshot?> GetSnapshotAsync(CancellationToken cancellationToken);
    Task InsertAsync(Guid snapshot, long index, ExactInstrumentDefinition definition, CancellationToken cancellationToken);
    Task PublishAsync(InstrumentDefinitionSnapshot snapshot, IReadOnlyCollection<TradeStrategyProduct> products, CancellationToken cancellationToken);
    Task<TomasAI.IFM.Domain.MarketData.Shared.ViewModels.TradeStrategySymbolReadModel[]> GetSymbolsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken);
    Task<IReadOnlyList<TradeStrategyProduct>> GetProductsAsync(Guid snapshot, TradeStrategyFamilyType family, CancellationToken cancellationToken);
}
