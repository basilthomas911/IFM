using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command;

public static class CancelOrderExecution
{
    /// <summary>Records authoritative cancellation for a cancellable execution.</summary>
    public static ServiceResult<GuidResult> Execute(this CancelOrderExecutionCommand command,
        OrderExecutionCommandState state) => OrderExecutionCommandModel.Cancel(command, state);
}
