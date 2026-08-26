using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTradeSessionBarPublisher.Command.Actor;

/// <summary>Defines readonly services required by the trade-session bar publisher Command actor.</summary>
public interface IFuturesTradeSessionBarPublisherCommandContext
    : ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor>
{
    /// <summary>Gets the typed event-source repository.</summary>
    IEventSourceActorStateRepository<FuturesTradeSessionBarPublisherCommandState> Repository { get; }
    /// <summary>Gets the durable bar event projector.</summary>
    IEventProjector<FuturesTradeSessionBarPublisherCommandActor> EventProjector { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesTradeSessionBarPublisherCommandActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the trade-session bar publisher Command actor.</summary>
public sealed class FuturesTradeSessionBarPublisherCommandContext
    : CommandActorContext,
      ICommandActorContext<FuturesTradeSessionBarPublisherCommandActor>,
      IFuturesTradeSessionBarPublisherCommandContext
{
    /// <summary>Initializes the immutable Command context.</summary>
    public FuturesTradeSessionBarPublisherCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorStateRepository<FuturesTradeSessionBarPublisherCommandState> repository,
        IEventProjector<FuturesTradeSessionBarPublisherCommandActor> eventProjector,
        ILogger<FuturesTradeSessionBarPublisherCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesTradeSessionBarPublisherCommandActor.ActorName))
    {
        Repository = IsArgumentNull.Set(repository);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IEventSourceActorStateRepository<FuturesTradeSessionBarPublisherCommandState> Repository { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesTradeSessionBarPublisherCommandActor> EventProjector { get; }
    /// <inheritdoc />
    public ILogger<FuturesTradeSessionBarPublisherCommandActor> Logger { get; }
}
