using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;

public static class RejectOrderExecution
{
    /// <summary>Records an authoritative broker rejection before any fill is accepted.</summary>
    public static ServiceResult<GuidResult> Execute(this RejectOrderExecutionCommand command,
        OrderExecutionCommandState state) => OrderExecutionCommandModel.Apply(command, state,
        static machine => machine.RejectExecution());
}
