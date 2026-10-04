using TomasAI.IFM.Domain.Trade.Order.Command.Model;
using TomasAI.IFM.Domain.Trade.Order.Command.State;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Command;

/// <summary>Handles BindTradeOrderExecution through computation, guards, and state-owned event application.</summary>
public static class BindTradeOrderExecution
{
    /// <summary>Computes the proposed order change and applies its event only when valid.</summary>
    /// <param name="command">The concrete BindTradeOrderExecution business intent.</param>
    /// <param name="state">The authoritative state owning the Trade Order definition.</param>
    /// <returns>The command ID on success, or its computation/application failure.</returns>
    public static ServiceResult<GuidResult> Execute(this BindTradeOrderExecutionCommand command, TradeOrderCommandState state)
    {
        var errorMsg = $"{command.CommandName}: unable to apply valid Trade Order change event";
        var updated = command.Compute(state.TradeOrderDefinition, out var tradeOrderChange) switch
        {
            _ when !tradeOrderChange.Accepted
                => command.UpdateFailed(ref errorMsg, $"{tradeOrderChange.RejectionCode};{tradeOrderChange.RejectionReason}"),
            _ when !tradeOrderChange.IsValidFor(command.EntityId)
                => command.UpdateFailed(ref errorMsg, $"{command.CommandName}: computed Trade Order definition is invalid for this order"),
            _ => state.Update(command.CreateTradeOrderChangedEvent(tradeOrderChange), command)
        };
        return updated
            ? new ServiceOk<GuidResult>(new GuidResult(command.CommandId))
            : command.UpdateFailed(errorMsg);
    }

    /// <summary>Computes domain data without changing the supplied definition or actor state.</summary>
    /// <param name="command">The concrete command containing the proposed business change.</param>
    /// <param name="tradeOrderDefinition">The definition currently owned by actor state.</param>
    /// <param name="tradeOrderChange">The computed definition or the domain rejection reason.</param>
    /// <returns>True when computation accepted the proposed change.</returns>
    internal static bool Compute(this BindTradeOrderExecutionCommand command, TradeOrderDefinition? tradeOrderDefinition,
        out TradeOrderCompute tradeOrderChange)
    {
        tradeOrderChange = TradeOrderComputation.Bind(tradeOrderDefinition, command.ExecutionAttemptId, command.ExecutionChannel, command.EffectiveAtUtc);
        return tradeOrderChange.Accepted;
    }

    /// <summary>Creates an event carrying the guarded business definition; does not mutate state.</summary>
    /// <param name="command">The originating command and its actor identity.</param>
    /// <param name="tradeOrderChange">The accepted and guarded computation.</param>
    /// <returns>The private source event to apply and persist through command state.</returns>
    internal static TradeOrderChangedEvent CreateTradeOrderChangedEvent(this BindTradeOrderExecutionCommand command,
        TradeOrderCompute tradeOrderChange) => new()
    {
        CommandId = command.CommandId,
        Subject = new(ActorType.Event, TradeOrderChangedEvent.Actor, TradeOrderChangedEvent.Verb, command.EntityId.Format()),
        EntityId = command.EntityId,
        ExecutionAttemptId = command.ExecutionAttemptId,
        ExecutionChannel = command.ExecutionChannel,
        TradeOrderDefinition = tradeOrderChange.TradeOrderDefinition!
    };
}
