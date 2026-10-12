using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesVwapSignal.Command.Actor;

/// <summary>Defines readonly services required by the VWAP Command actor.</summary>
public interface IFuturesVwapSignalCommandContext : ICommandActorContext<FuturesVwapSignalCommandActor>
{
    /// <summary>Gets the event-sourced VWAP state repository.</summary>
    IEventSourceActorStateRepository<FuturesVwapSignalCommandState> StateRepository { get; }
    /// <summary>Gets the command logger.</summary>
    ILogger<FuturesVwapSignalCommandActor> Logger { get; }
    /// <summary>Gets the event-source database context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the command-owned event projector.</summary>
    IEventProjector<FuturesVwapSignalCommandActor> EventProjector { get; }
}
