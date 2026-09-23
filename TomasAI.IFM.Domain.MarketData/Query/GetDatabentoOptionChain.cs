using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;

public static class GetDatabentoOptionChain
{
    public static async ValueTask ExecuteAsync(
        this GetDatabentoOptionChainQuery query,
        IMarketDataQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ServiceResult<FuturesOptionContractReadModel[]> result;
        var expiries = await context.DbFactory.SecuritiesDb.GetOptionContractExpiriesAsync(
            query.UnderlyingSymbol, query.MaturityDate, query.MaturityDate, cancellationToken).ConfigureAwait(false);
        var underlyingId = expiries.FirstOrDefault(row => row.ExpiryDate == query.MaturityDate
            && string.Equals(row.ProviderRoot, query.ProviderRoot, StringComparison.OrdinalIgnoreCase))?.ContractId;
        if (string.IsNullOrWhiteSpace(underlyingId))
            result = new ServiceFailed<FuturesOptionContractReadModel[]>(404, "The selected expiry is not present in the published option-definition cache.");
        else
        {
            var cached = await context.DbFactory.SecuritiesDb.GetCachedOptionContractDefinitionsAsync(
                query.UnderlyingSymbol, underlyingId, query.MaturityDate, [query.ProviderRoot], cancellationToken)
                .ConfigureAwait(false);
            var (currentPrice, deviation, observationDate) = await OptionChainWindowInputs.GetAsync(
                context, query.UnderlyingSymbol, underlyingId, cancellationToken).ConfigureAwait(false);
            var window = OptionChainStrikeWindow.Select(cached.Select(row => row.Definition),
                currentPrice, deviation, 2.5, 80);
            context.Logger.LogInformation(
                "Cached option chain {Root} {Expiry}: {Selected} of {Available} contracts in {Method}; current futures price {Price}, daily Bollinger deviation {Deviation} from {ObservationDate}.",
                query.ProviderRoot, query.MaturityDate, window.Contracts.Length, cached.Count,
                window.Method, currentPrice, deviation, observationDate);
            result = new ServiceOk<FuturesOptionContractReadModel[]>(window.Contracts);
        }
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }
}
