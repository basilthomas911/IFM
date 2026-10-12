using Microsoft.Extensions.Logging;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ServiceApi;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.MarketOutlookSnapshot.Extensions;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Shared.EventSourcing;
using System.Diagnostics;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Logging;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event;
/// <summary>Provides the FuturesItiSignalGeneratedComplete implementation.</summary>
public static class FuturesItiSignalGeneratedComplete
{
    /// <summary>
    /// Handles the completion of the Futures ITI signal generation process. It retrieves necessary data, updates the trade signal, and logs any errors that occur during the process.
    /// </summary>
    /// <param name = "e">The event instance containing details required for generating the futures trade signal, including the entity
    /// identifier.</param>
    /// <param name = "context">The context in which the event is processed, supplying information necessary for asynchronous operations.</param>
    /// <param name = "statusConsoleWriter">The writer used to output status messages to the console.</param>
    /// <param name = "logger">The logger used to log error messages.</param>
    /// <returns>A value indicating whether the execution completed successfully. Returns <see langword="true"/> if the operation
    /// succeeded; otherwise, <see langword="false"/>.</returns>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This realtime message boundary records the failed signal operation and contains it according to the existing telemetry and completion contract.")]
    public static async ValueTask<bool> ExecuteAsync(this FuturesItiSignalGeneratedCompleteEvent e, IEventActorContext<FuturesItiSignalEventActor> context, IStatusConsoleWriter statusConsoleWriter, ILogger<FuturesItiSignalEventActor> logger, FuturesItiSignalRuntimeTelemetry? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(statusConsoleWriter);
        var started = Stopwatch.GetTimestamp();
        FuturesItiSignalEventLogging.CompletionReceived(logger, e.Id, e.CommandId, e.EntityId.ContractId, e.EntityId.ValueDate, e.EntityId.TimePeriod);
        try
        {
            await e.PublishUpdatedNotificationAsync(context, logger).ConfigureAwait(false);
            await context.PublishMarketOutlookComponentAsync(e).ConfigureAwait(false);
            var derivedPeriodsSucceeded = await GenerateDerivedPeriodsAsync(e, context, logger).ConfigureAwait(false);
            await StartStrategyWorkflowAsync(e, context, logger, telemetry).ConfigureAwait(false);
            if (!FuturesTradeSignalPrerequisites.ShouldGenerate(e))
            {
                telemetry?.RecordCompletionHandled();
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
                {
                    var logValue1 = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
FuturesItiSignalEventLogging.CompletionHandled(logger, e.Id, e.CommandId, e.EntityId.ContractId, e.EntityId.ValueDate, e.EntityId.TimePeriod, true, derivedPeriodsSucceeded, logValue1);
                }

                return derivedPeriodsSucceeded;
            }

            var prerequisites = await FuturesTradeSignalPrerequisites.LoadAsync(e, context).ConfigureAwait(false);
            if (prerequisites.Inputs is not { } inputs)
            {
                global::TomasAI.IFM.Domain.MarketData.Analytics.ActorLifecycleDiagnosticLog.Write57(logger, null, e.EntityId.ContractId, e.EntityId.ValueDate, prerequisites.MissingInputs);
                telemetry?.RecordCompletionHandled();
                if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
                {
                    var logValue2 = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
FuturesItiSignalEventLogging.CompletionHandled(logger, e.Id, e.CommandId, e.EntityId.ContractId, e.EntityId.ValueDate, e.EntityId.TimePeriod, true, derivedPeriodsSucceeded, logValue2);
                }

                return true;
            }

            await (MarketDataAnalyticsCommandApiExtensions.UpdateFuturesTradeSignalAsync(context, inputs.FuturesEodData, inputs.FuturesRsiSignal, inputs.FuturesTdiSignal, inputs.FuturesItiSignalData, inputs.VixFuturesPrice, FuturesTradeSignalPrerequisites.SignalTimePeriod)).ConfigureAwait(false);
            telemetry?.RecordCompletionHandled();
            if (logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
            {
                var logValue3 = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
FuturesItiSignalEventLogging.CompletionHandled(logger, e.Id, e.CommandId, e.EntityId.ContractId, e.EntityId.ValueDate, e.EntityId.TimePeriod, true, derivedPeriodsSucceeded, logValue3);
            }

            return derivedPeriodsSucceeded;
        }
        catch (Exception ex)
        {
            telemetry?.RecordFailure(ex.Message);
            var logValue4 = ex.GetType().Name;
FuturesItiSignalEventLogging.CompletionFailed(logger, ex, e.Id, e.CommandId, e.EntityId.ContractId, e.EntityId.ValueDate, e.EntityId.TimePeriod, nameof(FuturesItiSignalGeneratedComplete), logValue4);
            await (statusConsoleWriter.WriteConsoleAsync(LogSourceType.FuturesItiSignalEvent, FuturesItiSignalGeneratedCompleteEvent.ErrorCode, ex.Message)).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>
    /// Requests Weekly and Monthly ITI generation from a persisted Daily completion.
    /// Completed Weekly and Monthly events intentionally produce no child commands.
    /// </summary>
    internal static async ValueTask<bool> GenerateDerivedPeriodsAsync(FuturesItiSignalGeneratedCompleteEvent completed, IEventActorContext context, ILogger<FuturesItiSignalEventActor> logger)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        if (completed.EntityId.TimePeriod != TimeFrameType.Daily)
        {
            return true;
        }

        var signal = completed.FuturesItiSignal ?? throw new InvalidOperationException("A Daily ITI completion must contain its persisted signal snapshot.");
        if (signal.TimePeriod != TimeFrameType.Daily || !StringComparer.Ordinal.Equals(signal.ContractId, completed.EntityId.ContractId) || signal.ValueDate != completed.EntityId.ValueDate)
        {
            throw new InvalidOperationException("The Daily ITI completion identity does not match its signal snapshot.");
        }

        var succeeded = true;
        foreach (var period in FuturesItiSignalTimeFrame.DerivedPeriods)
        {
            var frameStart = FuturesItiSignalTimeFrame.GetCalendarBucketStart(signal.ValueDate, period);
            var commandId = FuturesItiSignalTimeFrame.CreateDerivedCommandId(completed, period);
            FuturesItiSignalEventLogging.DerivedCommandRequested(logger, completed.Id, commandId, signal.ContractId, signal.ValueDate, period);
            var result = await MarketDataAnalyticsCommandApiExtensions.GenerateFuturesItiSignalAsync(context, signal.ContractId, signal.ValueDate, period, signal.IntrinsicTime, signal.IntrinsicPrice, completed.VixFuturesPrice, commandId, frameStart).ConfigureAwait(false);
            if (result is not ServiceFailed<GuidResult> failed)
            {
                FuturesItiSignalEventLogging.DerivedCommandAccepted(logger, completed.Id, commandId, signal.ContractId, signal.ValueDate, period);
                continue;
            }

            succeeded = false;
            FuturesItiSignalEventLogging.DerivedCommandRejected(logger, completed.Id, commandId, signal.ContractId, signal.ValueDate, period, failed.ErrorCode, failed.ErrorMessage ?? "Derived Generate command was rejected.");
        }

        return succeeded;
    }

    /// <summary>
    /// Starts the strategy workflow from the durable ITI completion. This replaces
    /// the removed realtime ITI source route, so only a successfully projected
    /// Generate event can enter the strategy workflow.
    /// </summary>
    internal static ValueTask StartStrategyWorkflowAsync(FuturesItiSignalGeneratedCompleteEvent completed, IEventActorContext context, ILogger? logger = null, FuturesItiSignalRuntimeTelemetry? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(completed);
        ArgumentNullException.ThrowIfNull(context);
        if (context is IFuturesItiSignalEventContext eventContext && !eventContext.WorkflowStartPolicy.Enabled)
        {
            return ValueTask.CompletedTask;
        }

        var signal = completed.FuturesItiSignal ?? throw new InvalidOperationException("An ITI Generate completion must contain its persisted signal snapshot.");
        if (signal.EntityId != completed.EntityId)
        {
            throw new InvalidOperationException("The ITI Generate completion identity does not match its signal snapshot.");
        }

        var triggerId = completed.CommandId;
        if (triggerId == Guid.Empty)
        {
            throw new InvalidOperationException("The ITI Generate completion requires a command identifier.");
        }

        var trigger = new FuturesItiSignalGeneratedEvent
        {
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor, FuturesItiSignalGeneratedEvent.Verb, completed.EntityId.Format()),
            Id = triggerId,
            EntityId = completed.EntityId,
            EventId = completed.EventId,
            CommandId = completed.CommandId,
            AggregateId = completed.AggregateId,
            EventSource = completed.EventSource,
            ReceivedOn = completed.ReceivedOn,
            FuturesItiSignal = signal,
            CreatedOn = completed.CreatedOn,
            CreatedBy = completed.CreatedBy,
            VixFuturesPrice = completed.VixFuturesPrice,
            DeriveLongerPeriods = false
        };
        var workflowEntityId = IntrinsicTimeStrategyWorkflowEntityId.Create(completed.EntityId);
        var requestedAtUtc = DateTime.UtcNow;
        var command = new ExecuteIntrinsicTimeStrategyWorkflowCommand
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, ExecuteIntrinsicTimeStrategyWorkflowCommand.Actor, ExecuteIntrinsicTimeStrategyWorkflowCommand.Verb, workflowEntityId.Format()),
            EntityId = workflowEntityId,
            ProposedWorkflowId = StrategyWorkflowId.New(TimeProvider.System),
            TriggerEventId = triggerId,
            TriggerEvent = trigger,
            CorrelationId = completed.CommandId == Guid.Empty ? triggerId : completed.CommandId,
            CausationId = triggerId,
            RequestedAtUtc = requestedAtUtc,
            WorkflowDefinitionVersion = 1
        };
        telemetry?.RecordWorkflowRequested();
        if (logger is not null && logger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Information))
        {
                var logValue1 = command.ProposedWorkflowId.ToString();
var logValue2 = workflowEntityId.Format();
FuturesItiSignalEventLogging.WorkflowRequested(logger, triggerId, command.CommandId, logValue1, logValue2, signal.ContractId, signal.ValueDate, signal.TimePeriod);
        }

        return context.SendAsync<ExecuteIntrinsicTimeStrategyWorkflowCommand, IntrinsicTimeStrategyWorkflowEntityId>(command, workflowEntityId);
    }
}
