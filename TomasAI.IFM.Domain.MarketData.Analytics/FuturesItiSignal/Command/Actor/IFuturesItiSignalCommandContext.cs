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
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Validation;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.MarketData.OperationsHealth;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesItiSignalCommandActor"/>.</summary>
public interface IFuturesItiSignalCommandContext : ICommandActorContext<FuturesItiSignalCommandActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the DbEventSource service supplied to the actor context.</summary>
    IEventSourceActorDbContext DbEventSource { get; }
    /// <summary>Gets the EventProjector service supplied to the actor context.</summary>
    IEventProjector<FuturesItiSignalCommandActor> EventProjector { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesItiSignalCommandActor> Logger { get; }
    /// <summary>Gets bounded Futures ITI runtime telemetry.</summary>
    FuturesItiSignalRuntimeTelemetry Telemetry { get; }
}
