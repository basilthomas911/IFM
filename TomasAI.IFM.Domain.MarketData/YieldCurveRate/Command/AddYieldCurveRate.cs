using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.YieldCurveRate.Command;

/// <summary>Handles AddYieldCurveRate through computation, failure guards and state-owned event application.</summary>
public static class AddYieldCurveRate
{
    /// <summary>Applies the computed business data only when ownership and lifecycle guards succeed.</summary>
    /// <param name="command">The concrete YieldCurveRate intent.</param>
    /// <param name="state">The authoritative YieldCurveRate state.</param>
    /// <returns>The command ID on success, or the guarded/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this AddYieldCurveRateCommand command, YieldCurveRateCommandState state)
    {
        var errorMsg = "YieldCurveRate.STATE.APPLY_FAILED";
        var computed = command.Compute(out var yieldCurveRate);
        var updated = computed switch
        {
            _ when !computed => command.UpdateFailed(ref errorMsg, "YieldCurveRate.COMPUTED_DATA.INVALID"),
            _ when state.YieldCurveRateExists(yieldCurveRate.ValueDate, command.Overwrite)
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: yield curve rate for {yieldCurveRate.ValueDate:yyyy-MMM-dd} already exists"),
            _ => state.Update(command.CreateYieldCurveRateAddedEvent(yieldCurveRate), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes business data without modifying actor state or command inputs.</summary>
    /// <param name="command">The proposed YieldCurveRate change.</param>
    /// <param name="yieldCurveRate">The computed business data to carry in its event.</param>
    /// <returns>True when the computed data belongs to the commanded entity.</returns>
    internal static bool Compute(this AddYieldCurveRateCommand command, out YieldCurveRateReadModel yieldCurveRate)
    {
        yieldCurveRate = command.YieldCurveRate is { } proposedYieldCurveRate ? proposedYieldCurveRate with { } : null!;
        return yieldCurveRate is not null && yieldCurveRate.ValueDate != default && command.EntityId is not null && yieldCurveRate.ValueDate.Year == command.EntityId.Year;
    }

    /// <summary>Creates the private event from guarded business data without changing state.</summary>
    /// <param name="command">The originating command and route.</param>
    /// <param name="yieldCurveRate">The computed, guarded business data.</param>
    /// <returns>The source event applied and persisted through State.Update.</returns>
    internal static YieldCurveRateAddedEvent CreateYieldCurveRateAddedEvent(this AddYieldCurveRateCommand command, YieldCurveRateReadModel yieldCurveRate) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, YieldCurveRateAddedEvent.Actor, YieldCurveRateAddedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        YieldCurveRate = yieldCurveRate,
        CreatedOn = command.OriginatedOn,
        CreatedBy = command.OriginatedBy
    };
}
