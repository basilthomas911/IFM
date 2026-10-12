using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Shared.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.State;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Validation;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesTdiSignalCommandActor"/>.</summary>
public interface IFuturesTdiSignalCommandContext : ICommandActorContext<FuturesTdiSignalCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<FuturesTdiSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesTdiSignalCommandActor> Logger { get; }
}
