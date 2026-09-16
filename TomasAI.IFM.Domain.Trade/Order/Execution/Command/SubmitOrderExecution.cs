using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;

public static class SubmitOrderExecution
{
    /// <summary>Marks a pending execution as submitted after its BrokerOrder intents are committed.</summary>
    public static ServiceResult<GuidResult> Execute(this SubmitOrderExecutionCommand command,
        OrderExecutionCommandState state) => OrderExecutionCommandModel.Apply(command, state,
        static machine => machine.MarkSubmitted());
}
