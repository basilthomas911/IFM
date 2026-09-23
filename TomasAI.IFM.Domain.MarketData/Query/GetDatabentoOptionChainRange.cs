using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Query;

/// <summary>Handles provider-backed Databento option-chain range discovery.</summary>
public static class GetDatabentoOptionChainRange
{
    public static async ValueTask ExecuteAsync(
        this GetDatabentoOptionChainRangeQuery query,
        IMarketDataQueryContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contracts = await context.DbFactory.SecuritiesDb.GetOptionContractExpiriesAsync(
            query.UnderlyingSymbol,
            query.FromMaturityDate,
            query.ThroughMaturityDate,
            cancellationToken).ConfigureAwait(false);
        ServiceResult<OptionContractExpiryReadModel[]> result =
            new ServiceOk<OptionContractExpiryReadModel[]>(contracts.ToArray());
        cancellationToken.ThrowIfCancellationRequested();
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }
}
