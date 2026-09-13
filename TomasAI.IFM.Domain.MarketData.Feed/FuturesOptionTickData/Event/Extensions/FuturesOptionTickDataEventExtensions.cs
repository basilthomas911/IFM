using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionTickData.Event.Extensions;

/// <summary>
/// Provides extension methods on <see cref="IEventActorContext"/> that encapsulate outgoing
/// messages (events, queries, and commands) originating from the futures-option tick-data
/// event actor.
/// </summary>
/// <remarks>
/// Each method constructs the appropriate actor message with its <see cref="ActorSubject"/>,
/// entity identifier, and payload, then dispatches it through the actor messaging
/// infrastructure. This keeps message-construction details out of the event handlers
/// themselves.
/// </remarks>
internal static class FuturesOptionTickDataEventExtensions
{
    /// <summary>
    /// Queries the market-data-feed actor for the streaming request identifier associated
    /// with the specified request key.
    /// </summary>
    /// <remarks>This method performs an asynchronous request via the actor messaging
    /// infrastructure. If the query fails or returns no data, the method returns
    /// <c>0</c>.</remarks>
    /// <param name="context">The event actor context used to issue the query.</param>
    /// <param name="requestKey">The key that uniquely identifies the streaming request
    /// (typically a contract identifier).</param>
    /// <returns>A value task whose result is the streaming request identifier, or <c>0</c>
    /// if the query was unsuccessful.</returns>
    internal static async ValueTask<int> GetStreamingRequestIdQueryAsync(this IEventActorContext context, string requestKey)
    {
        var streamingRequestId = 0;
        var entityId = new GetStreamingRequestIdParameter();
        GetStreamingRequestIdQuery query = new(requestKey)
        {
            Subject = new ActorSubject(ActorType.Query, GetStreamingRequestIdQuery.Actor, GetStreamingRequestIdQuery.Verb, entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetStreamingRequestIdQuery.ErrorId
        };
        var serviceResult = await context.RequestAsync<ScalarValue<int>, GetStreamingRequestIdQuery>(query);
        if (serviceResult.Success && serviceResult.Value is not null)
            streamingRequestId = serviceResult.Value.Value;
        return streamingRequestId;
    }
}
