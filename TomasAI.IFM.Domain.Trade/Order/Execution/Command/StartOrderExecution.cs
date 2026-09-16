using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;

public static class StartOrderExecution
{
    /// <summary>Starts one execution only when the approved order and attempt are valid.</summary>
    public static ServiceResult<GuidResult> Execute(this StartOrderExecutionCommand command,
        OrderExecutionCommandState state) => OrderExecutionCommandModel.Apply(command, state,
        machine => machine.Start(command.Order, command.ExecutionAttemptId, command.Channel, command.EffectiveAtUtc));
}
