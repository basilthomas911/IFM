using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="GetTradeStrategySymbolsQuery"/>.</summary>
public static class GetTradeStrategySymbols
{

    extension(GetTradeStrategySymbolsQuery query)
    {
        /// <summary>Reads and returns symbols for the requested strategy family.</summary>
        public async ValueTask<ServiceResult<TradeStrategySymbolReadModel[]>> ExecuteAsync(IReferenceQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var database = context.DbFactory.ReferenceDb;
            var snapshot = await database.GetInstrumentDefinitionSnapshotAsync(cancellationToken).ConfigureAwait(false);
            ServiceResult<TradeStrategySymbolReadModel[]> result = snapshot is null
                ? new ServiceFailed<TradeStrategySymbolReadModel[]>(GetTradeStrategySymbolsQuery.ErrorId, "No published instrument-definition snapshot is available.")
                : new ServiceOk<TradeStrategySymbolReadModel[]>(await database.GetInstrumentDefinitionSymbolsAsync(snapshot.Id, query.Family, cancellationToken).ConfigureAwait(false));
            return result;
        }
    }
}
