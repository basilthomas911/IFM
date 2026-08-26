using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Command.Actor;

/// <summary>Defines readonly services required by the history-bootstrap Command actor.</summary>
public interface IFuturesAnalyticsHistoryBootstrapCommandContext
    : ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor>
{
    /// <summary>Gets the typed state repository.</summary>
    IEventSourceActorStateRepository<FuturesAnalyticsHistoryBootstrapCommandState> Repository { get; }
    /// <summary>Gets the durable event projector.</summary>
    IEventProjector<FuturesAnalyticsHistoryBootstrapCommandActor> EventProjector { get; }
    /// <summary>Gets the typed logger.</summary>
    ILogger<FuturesAnalyticsHistoryBootstrapCommandActor> Logger { get; }
}

/// <summary>Provides the closed generic context for the history-bootstrap Command actor.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapCommandContext
    : CommandActorContext,
      ICommandActorContext<FuturesAnalyticsHistoryBootstrapCommandActor>,
      IFuturesAnalyticsHistoryBootstrapCommandContext
{
    /// <summary>Initializes the readonly context.</summary>
    public FuturesAnalyticsHistoryBootstrapCommandContext(
        IActorSupervisor supervisor,
        IEventSourceActorStateRepository<FuturesAnalyticsHistoryBootstrapCommandState> repository,
        IEventProjector<FuturesAnalyticsHistoryBootstrapCommandActor> eventProjector,
        ILogger<FuturesAnalyticsHistoryBootstrapCommandActor> logger)
        : base(supervisor, new(ActorType.Command, FuturesAnalyticsHistoryBootstrapCommandActor.ActorName))
    {
        Repository = IsArgumentNull.Set(repository);
        EventProjector = IsArgumentNull.Set(eventProjector);
        Logger = IsArgumentNull.Set(logger);
    }

    /// <inheritdoc />
    public IEventSourceActorStateRepository<FuturesAnalyticsHistoryBootstrapCommandState> Repository { get; }
    /// <inheritdoc />
    public IEventProjector<FuturesAnalyticsHistoryBootstrapCommandActor> EventProjector { get; }
    /// <inheritdoc />
    public ILogger<FuturesAnalyticsHistoryBootstrapCommandActor> Logger { get; }
}
