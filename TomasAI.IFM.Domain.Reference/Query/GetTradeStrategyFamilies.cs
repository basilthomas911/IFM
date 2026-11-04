using TomasAI.IFM.Domain.Reference.Query.Actor;
using TomasAI.IFM.Domain.Reference.Query.Extensions;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Query;

/// <summary>Handles <see cref="GetTradeStrategyFamiliesQuery"/>.</summary>
public static class GetTradeStrategyFamilies
{

    extension(GetTradeStrategyFamiliesQuery query)
    {
        /// <summary>Reads and returns all configured strategy families.</summary>
        public async ValueTask<ServiceResult<TradeStrategyFamilyReadModel[]>> ExecuteAsync(IReferenceQueryContext context, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            var result = await (context.GetTradeStrategyFamiliesAsync(cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }
}
