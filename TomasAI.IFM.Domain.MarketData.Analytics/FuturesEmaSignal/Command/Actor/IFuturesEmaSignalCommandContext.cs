using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesEmaSignal.Command.Actor;

/// <summary>Defines readonly services required by the EMA command actor.</summary>
public interface IFuturesEmaSignalCommandContext : ICommandActorContext<FuturesEmaSignalCommandActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesEmaSignalCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesEmaSignalCommandActor> EventProjector { get; }
}
