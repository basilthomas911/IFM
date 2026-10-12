using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesBbSignal.Command.Actor;

/// <summary>Defines readonly services required by the Bollinger command actor.</summary>
public interface IFuturesBbSignalCommandContext : ICommandActorContext<FuturesBbSignalCommandActor>
{
    /// <summary>Gets the actor logger.</summary>
    ILogger<FuturesBbSignalCommandActor> Logger { get; }
    /// <summary>Gets the event-source database.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the event projector.</summary>
    IEventProjector<FuturesBbSignalCommandActor> EventProjector { get; }
}
