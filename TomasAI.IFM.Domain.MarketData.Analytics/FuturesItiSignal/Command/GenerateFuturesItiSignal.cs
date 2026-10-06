using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.State;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Logging;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command;

/// <summary>Owns the concrete command handler and event factories for this analytics operation.</summary>
public static class GenerateFuturesItiSignal
{
    /// <summary>Computes and validates the Futures Iti Signal command, then applies accepted events through actor-owned state.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="state">The command actor state that owns the current business values and pending events.</param>
    /// <param name="telemetry">The optional ITI runtime counters for accepted changes and no-change decisions.</param>
    /// <param name="logger">The optional structured logger for ITI evaluation and outcome boundaries.</param>
    /// <returns>The originating command ID on acceptance, including an idempotent no-change result; otherwise, the business rejection or state-application failure.</returns>
    public static ServiceResult<GuidResult> Execute(
        this GenerateFuturesItiSignalCommand command,
        FuturesItiSignalCommandState state,
        FuturesItiSignalRuntimeTelemetry? telemetry = null,
        ILogger? logger = null)
    {
        if (logger is not null)
            FuturesItiSignalCommandLogging.Evaluating(
                logger, command.CommandId, command.EntityId.Format(), command.ContractId, command.ValueDate, command.TimePeriod);
        if (!command.Compute(state.FuturesItiSignal, out var futuresItiSignal))
        {
            telemetry?.RecordNoChange();
            if (logger is not null)
                FuturesItiSignalCommandLogging.NoChange(
                    logger, command.CommandId, command.EntityId.Format(), command.ContractId, command.ValueDate, command.TimePeriod);
            return new ServiceOk<GuidResult>(new GuidResult(command.CommandId));
        }

        var errorMsg = "unable to apply generated ITI signal event";
        var updated = futuresItiSignal switch
        {
            _ when futuresItiSignal is null
                => command.UpdateFailed(ref errorMsg, "computed ITI signal is missing"),
            _ => state.Update(command.CreateFuturesItiSignalGeneratedEvent(futuresItiSignal), command)
        };
        if (updated)
        {
            telemetry?.RecordSignalChanged();
            if (logger is not null)
                FuturesItiSignalCommandLogging.SignalChanged(
                    logger, command.CommandId, command.EntityId.Format(), command.ContractId, command.ValueDate, command.TimePeriod);
        }
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed($"{command.CommandName}: {errorMsg}");
    }

    /// <summary>Computes the proposed Futures Iti Signal result from the supplied business inputs without mutating actor state or pending events.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="currentFuturesItiSignal">The previously accepted current futures iti signal used only as computation input.</param>
    /// <param name="futuresItiSignal">The futures iti signal business data used by this operation.</param>
    /// <returns>True when the proposed business result advances or is accepted; otherwise, false. The output retains the no-change or rejection decision.</returns>
    /// <remarks>Accumulator validation exceptions propagate to the command actor exception boundary.</remarks>
    internal static bool Compute(this GenerateFuturesItiSignalCommand command, FuturesItiSignalV2ReadModel? currentFuturesItiSignal, out FuturesItiSignalV2ReadModel futuresItiSignal)
        => FuturesItiSignalCompute.TryCompute(command, currentFuturesItiSignal, out futuresItiSignal);

    /// <summary>Creates the Futures Iti Signal event payload from accepted business data without changing state or publishing messages.</summary>
    /// <param name="command">The originating concrete command, including its identity and domain inputs.</param>
    /// <param name="futuresItiSignal">The futures iti signal business data used by this operation.</param>
    /// <returns>The event or ordered event collection to apply through actor state and persist before projection.</returns>
    internal static FuturesItiSignalGeneratedEvent CreateFuturesItiSignalGeneratedEvent(this GenerateFuturesItiSignalCommand command, FuturesItiSignalV2ReadModel futuresItiSignal)
        => new()
        {
            CommandId = command.CommandId,
            Subject = new ActorSubject(ActorType.Event, FuturesItiSignalGeneratedEvent.Actor, FuturesItiSignalGeneratedEvent.Verb, command.EntityId.Format()),
            EntityId = command.EntityId,
            FuturesItiSignal = futuresItiSignal,
            VixFuturesPrice = command.VixFuturesPrice,
            DeriveLongerPeriods = false,
            CreatedOn = command.OriginatedOn,
            CreatedBy = command.OriginatedBy
        };

}
