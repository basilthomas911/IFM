using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;

internal static class TradeSelectionFunctionTestDriver
{
    public static async Task<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>> ExecuteAsync(
        TradeSelectionFunctionActor actor, ExecuteTradeSelectionPipelineCommand command, CancellationToken token = default)
    {
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteTradeSelectionPipelineCommand>().Returns(command);
        ServiceResult<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>? reply = null;
        message.ReplyAsync(Arg.Do<ServiceResult<FunctionResult<TradeSelectionFunctionCompletedEvent, TradeSelectionFunctionFailedEvent>>>(value => reply = value))
            .Returns(ValueTask.CompletedTask);
        await actor.HandleMessageAsync(message, command.Subject.ThreadId, token);
        message.Received(1).ReleasePayload();
        return reply!.Value!;
    }
}
