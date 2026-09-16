using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;

public static class AcceptOrderExecution
{
    /// <summary>Accepts only complete or explicitly permitted balanced execution evidence.</summary>
    public static ServiceResult<GuidResult> Execute(this AcceptOrderExecutionCommand command,
        OrderExecutionCommandState state)
    {
        if (state.Current is { } current && current.Components.Any(component =>
                !current.Fills.Any(fill => fill.ComponentId == component.ComponentId)))
            return TradeCommandResult.Accepted(command.CommandId);

        return OrderExecutionCommandModel.Accept(command, state);
    }
}
