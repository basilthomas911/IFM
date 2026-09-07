using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Function.Actor;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

internal static class MarketConditionFunctionTestDriver
{
    public static async Task<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>> ExecuteAsync(
        MarketConditionFunctionActor actor, ExecuteMarketConditionAssessmentCommand command, CancellationToken token = default)
    {
        var message = Substitute.For<IActorMessage>();
        message.Subject.Returns(command.Subject);
        message.AsCommand<ExecuteMarketConditionAssessmentCommand>().Returns(command);
        ServiceResult<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>? reply = null;
        message.ReplyAsync(Arg.Do<ServiceResult<FunctionResult<MarketConditionAssessmentCompletedEvent, MarketConditionAssessmentFailedEvent>>>(value => reply = value))
            .Returns(ValueTask.CompletedTask);
        await actor.HandleMessageAsync(message, command.Subject.ThreadId, token);
        message.Received(1).ReleasePayload();
        return reply!.Value!;
    }
}
