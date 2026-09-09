using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;

internal static class RiskManagementFunctionTestDriver
{
    public static async Task<FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>> ExecuteAsync(
        RiskManagementFunctionActor actor, ExecuteRiskManagementPipelineCommand command, CancellationToken token = default)
    {
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteRiskManagementPipelineCommand>().Returns(command);
        ServiceResult<FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>? reply = null;
        message.ReplyAsync(Arg.Do<ServiceResult<FunctionResult<RiskManagementFunctionCompletedEvent, RiskManagementFunctionFailedEvent>>>(value => reply = value))
            .Returns(ValueTask.CompletedTask);
        await actor.HandleMessageAsync(message, command.Subject.ThreadId, token);
        message.Received(1).ReleasePayload();
        return reply!.Value!;
    }
}
