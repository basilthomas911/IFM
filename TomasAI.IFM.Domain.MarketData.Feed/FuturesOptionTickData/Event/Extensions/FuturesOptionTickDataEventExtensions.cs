using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Extensions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
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

    /// <param name="context">
    /// The event actor context used to issue the query.
    /// </param>
    extension(IEventActorContext context)
    {

    }
}
