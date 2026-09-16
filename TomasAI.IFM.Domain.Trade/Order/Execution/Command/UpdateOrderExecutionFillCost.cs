using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;

public static class UpdateOrderExecutionFillCost
{
    /// <summary>Applies a commission to the exact external execution evidence.</summary>
    public static ServiceResult<GuidResult> Execute(this UpdateOrderExecutionFillCostCommand command,
        OrderExecutionCommandState state) => OrderExecutionCommandModel.Apply(command, state,
        machine => machine.UpdateFillCost(command.ExternalExecutionId, command.Commission));
}
