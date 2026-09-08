using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Model;
using TomasAI.IFM.Shared.EventSourcing;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
public sealed class OrderCompositionFunctionTests
{
    [Fact,Trait("Fixture","OC-C25")]
    public async Task Success_projects_then_appends_and_replay_never_reprojects_even_after_expiry()
    {
        var c=await CompositionFixture.Command();var f=new CompositionFunctionFixture(c);
        var first=await f.Execute();first.IsCompleted.Should().BeTrue();f.Order.Should().Equal("load","project","persist");
        f.Clock.Now=c.ExpiresAtUtc.AddDays(1);var replay=await f.Execute();replay.Completed.Should().Be(first.Completed);f.Order.Should().Equal("load","project","persist","load");
        var conflict=await f.Execute(CompositionFixture.Seal(c with{CausationId=Guid.NewGuid()}));conflict.IsFailed.Should().BeTrue();conflict.Failed!.ReasonCode.Should().Be("OC.CONTRACT.CONFLICTING_DUPLICATE");
    }
    [Theory,InlineData("load"),InlineData("project"),InlineData("persist"),Trait("Fixture","OC-C24")]
    public async Task Failed_attempt_is_not_committed_and_identical_retry_can_complete(string stage)
    {
        var f=new CompositionFunctionFixture(await CompositionFixture.Command()){FailAt=stage};
        var result=await f.Execute();result.IsFailed.Should().BeTrue();f.Committed.Should().BeNull();
        if(stage=="project")f.Order.Should().NotContain("persist");
        f.FailAt="";(await f.Execute()).IsCompleted.Should().BeTrue();f.Committed.Should().NotBeNull();
    }
    [Fact,Trait("Fixture","OC-C23")]
    public async Task Caller_cancellation_releases_payload_without_projection_or_state()
    {
        var f=new CompositionFunctionFixture(await CompositionFixture.Command());using var cancellation=new CancellationTokenSource();cancellation.Cancel();
        Func<Task> act=()=>f.Execute(token:cancellation.Token);await act.Should().ThrowAsync<OperationCanceledException>();f.Committed.Should().BeNull();f.Order.Should().NotContain("project");
    }
    [Fact,Trait("Fixture","OC-C23")]
    public async Task Late_projection_is_observed_and_cannot_append_a_success()
    {
        var c=await CompositionFixture.Command();c=CompositionFixture.Seal(c with{ExpiresAtUtc=c.EvaluatedAtUtc.AddMilliseconds(15)});
        var f=new CompositionFunctionFixture(c);var late=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Project=()=>new ValueTask(late.Task);
        var result=await f.Execute();result.IsFailed.Should().BeTrue();result.Failed!.ReasonCode.Should().Be("OC.TIME.EXPIRED");
        late.SetResult();await Task.Yield();f.Committed.Should().BeNull();f.Order.Should().NotContain("persist");
    }
    [Theory,InlineData("Execute",true),InlineData("Start",false),InlineData("execute",false),InlineData("Unknown",false)]
    public async Task Exact_mailbox_verb_controls_dispatch_and_payload_is_always_released(string verb,bool success)
    {
        var c=await CompositionFixture.Command();var f=new CompositionFunctionFixture(c);
        var result=await f.Execute(CompositionFixture.Seal(c with{Subject=new(ActorType.Function,ExecuteOrderCompositionPipelineCommand.Actor,verb,c.EntityId.Format())}));
        result.IsCompleted.Should().Be(success);
    }
}
