using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;

internal static class OrderCompositionFunctionTestDriver
{
    public static async Task<FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>> ExecuteAsync(
        OrderCompositionFunctionActor actor, ExecuteOrderCompositionPipelineCommand command, CancellationToken token = default)
    {
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteOrderCompositionPipelineCommand>().Returns(command);
        ServiceResult<FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>? reply = null;
        message.ReplyAsync(Arg.Do<ServiceResult<FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>>(value => reply = value))
            .Returns(ValueTask.CompletedTask);
        await actor.HandleMessageAsync(message, command.Subject.ThreadId, token);
        message.Received(1).ReleasePayload();
        return reply!.Value!;
    }
}
