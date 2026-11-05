using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Handles provider-backed Databento option-chain range discovery.</summary>
public static class GetDatabentoOptionChainRange
{

    extension(GetDatabentoOptionChainRangeQuery query)
    {
        public async ValueTask<ServiceResult<OptionContractExpiryReadModel[]>> ExecuteAsync(
            IMarketDataQueryContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(query);
            cancellationToken.ThrowIfCancellationRequested();
            var contracts = await context.DbFactory.SecuritiesDb.GetOptionContractExpiriesAsync(
                query.UnderlyingSymbol,
                query.FromMaturityDate,
                query.ThroughMaturityDate,
                cancellationToken).ConfigureAwait(false);
            ServiceResult<OptionContractExpiryReadModel[]> result =
                new ServiceOk<OptionContractExpiryReadModel[]>(contracts.ToArray());
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
    }
}
