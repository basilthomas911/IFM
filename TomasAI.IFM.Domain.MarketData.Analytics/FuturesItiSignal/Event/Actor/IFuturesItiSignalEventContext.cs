using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;

/// <summary>Defines the readonly runtime services required by <see cref="FuturesItiSignalEventActor"/>.</summary>
public interface IFuturesItiSignalEventContext : IEventActorContext<FuturesItiSignalEventActor>
{
    /// <summary>Gets the Supervisor service supplied to the actor context.</summary>
    IActorSupervisor Supervisor { get; }
    /// <summary>Gets the StatusConsoleWriter service supplied to the actor context.</summary>
    IStatusConsoleWriter StatusConsoleWriter { get; }
    /// <summary>Gets the Logger service supplied to the actor context.</summary>
    ILogger<FuturesItiSignalEventActor> Logger { get; }
    /// <summary>Gets bounded Futures ITI runtime telemetry.</summary>
    FuturesItiSignalRuntimeTelemetry Telemetry { get; }
    /// <summary>Gets the admission policy for new ITI-triggered strategy workflows.</summary>
    IIntrinsicTimeWorkflowStartPolicy WorkflowStartPolicy { get; }
}
