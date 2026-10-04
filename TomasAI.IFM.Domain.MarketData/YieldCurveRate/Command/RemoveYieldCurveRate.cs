using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;

/// <summary>Handles RemoveYieldCurveRate through computation, failure guards and state-owned event application.</summary>
public static class RemoveYieldCurveRate
{
    /// <summary>Applies the computed business data only when ownership and lifecycle guards succeed.</summary>
    /// <param name="command">The concrete YieldCurveRate intent.</param>
    /// <param name="state">The authoritative YieldCurveRate state.</param>
    /// <returns>The command ID on success, or the guarded/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this RemoveYieldCurveRateCommand command, YieldCurveRateCommandState state)
    {
        var errorMsg = "YieldCurveRate.STATE.APPLY_FAILED";
        var computed = command.Compute(out var valueDate);
        var updated = computed switch
        {
            _ when !computed => command.UpdateFailed(ref errorMsg, "YieldCurveRate.COMPUTED_DATA.INVALID"),
            _ when state.YieldCurveRateDoesNotExist(valueDate, command.Overwrite)
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: yield curve rate for {valueDate:yyyy-MMM-dd} does not exist"),
            _ => state.Update(command.CreateYieldCurveRateRemovedEvent(valueDate), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes business data without modifying actor state or command inputs.</summary>
    /// <param name="command">The proposed YieldCurveRate change.</param>
    /// <param name="valueDate">The computed business data to carry in its event.</param>
    /// <returns>True when the computed data belongs to the commanded entity.</returns>
    internal static bool Compute(this RemoveYieldCurveRateCommand command, out DateOnly valueDate)
    {
        valueDate = command.ValueDate;
        return valueDate != default && command.EntityId is not null && valueDate.Year == command.EntityId.Year;
    }

    /// <summary>Creates the private event from guarded business data without changing state.</summary>
    /// <param name="command">The originating command and route.</param>
    /// <param name="valueDate">The computed, guarded business data.</param>
    /// <returns>The source event applied and persisted through State.Update.</returns>
    internal static YieldCurveRateRemovedEvent CreateYieldCurveRateRemovedEvent(this RemoveYieldCurveRateCommand command, DateOnly valueDate) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, YieldCurveRateRemovedEvent.Actor, YieldCurveRateRemovedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        ValueDate = valueDate,
        DeletedOn = command.OriginatedOn,
        DeletedBy = command.OriginatedBy
    };
}
