using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.OptionChainCache;

/// <summary>Queries persisted policy projections only; command actor state is never used as a read store.</summary>
public static class GetStrategyOptionChainParameters
{
    /// <summary>Reads the requested immutable version and replies through the query actor.</summary>
    public static async ValueTask ExecuteAsync(this GetStrategyOptionChainParametersQuery query, IMarketDataQueryContext context, CancellationToken token)
    {
        var parameters = await context.DbFactory.MarketDataDb.ReadVersionAsync(query.ParameterSetId, query.Version, token).ConfigureAwait(false);
        ServiceResult<StrategyOptionChainParameterSet> result = parameters is null
            ? new ServiceFailed<StrategyOptionChainParameterSet>(404, "Strategy option-chain parameter version not found.")
            : new ServiceOk<StrategyOptionChainParameterSet>(parameters);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, result).ConfigureAwait(false);
    }
}
