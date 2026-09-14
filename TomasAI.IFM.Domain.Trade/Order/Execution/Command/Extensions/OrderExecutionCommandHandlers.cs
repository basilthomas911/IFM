using TomasAI.IFM.Domain.Trade.Order.Execution.Command.State;
using TomasAI.IFM.Domain.Trade.Order.Execution.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Model;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Order.Execution.Command.Extensions;

public static class OrderExecutionCommandHandlers
{
    public static ServiceResult<GuidResult> Execute(this StartOrderExecutionCommand c,OrderExecutionCommandState s)=>Apply(c,s,m=>m.Start(c.Order,c.ExecutionAttemptId,c.Channel,c.EffectiveAtUtc));
    public static ServiceResult<GuidResult> Execute(this SubmitOrderExecutionCommand c,OrderExecutionCommandState s)=>Apply(c,s,static m=>m.MarkSubmitted());
    public static ServiceResult<GuidResult> Execute(this AddOrderExecutionFillCommand c,OrderExecutionCommandState s)=>Apply(c,s,m=>m.AddFill(c.Fill));
    public static ServiceResult<GuidResult> Execute(this CancelOrderExecutionCommand c,OrderExecutionCommandState s)=>Apply(c,s,static m=>m.Cancel());
    public static ServiceResult<GuidResult> Execute(this RejectOrderExecutionCommand c,OrderExecutionCommandState s)=>Apply(c,s,static m=>m.RejectExecution());
    public static ServiceResult<GuidResult> Execute(this AcceptOrderExecutionCommand c,OrderExecutionCommandState s)
    {
        var machine=Machine(s); var accepted=machine.Accept(c.EffectiveAtUtc);
        if(!accepted.Accepted||accepted.Value is null)return TradeCommandResult.Rejected(c.ErrorCode,accepted);
        var current=machine.Current!; s.Update(new OrderExecutionChangedEvent
        {
            EntityId=c.EntityId,State=current,CreatedTrades=accepted.Value.CreatedTrades,
            ClosedPositions=accepted.Value.ClosedPositions
        },c);
        return TradeCommandResult.Accepted(c.CommandId);
    }
    static ServiceResult<GuidResult> Apply(ICommand<OrderExecutionId> command,OrderExecutionCommandState state,Func<OrderExecutionActorStateMachine,TradeDecision<OrderExecutionDefinition>> transition)
    {
        var machine=Machine(state);var result=transition(machine);var raw=(ICommand)command;
        if(!result.Accepted||result.Value is null)return TradeCommandResult.Rejected(raw.ErrorCode,result);
        state.Update(new OrderExecutionChangedEvent{EntityId=command.EntityId,State=result.Value},raw);return TradeCommandResult.Accepted(raw.CommandId);
    }
    static OrderExecutionActorStateMachine Machine(OrderExecutionCommandState state){var m=new OrderExecutionActorStateMachine();if(state.Current is{} current)m.Replay(current);return m;}
}
