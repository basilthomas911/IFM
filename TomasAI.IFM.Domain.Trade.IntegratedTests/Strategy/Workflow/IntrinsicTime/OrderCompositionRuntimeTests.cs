using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.State;
using TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.OrderComposer;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

public sealed partial class TradeSelectionRuntimeTests
{
    [Theory, InlineData("LongFuture"), InlineData("BullCallDebit"), InlineData("LongBullishIronCondor"), Trait("Gate", "OC-05"), Trait("Gate", "OC-07"), Trait("Gate", "OC-08")]
    public async Task OrderComposer_real_NATS_Scylla_Postgres_completion_replay_conflict_and_queries(string variant)
    {
        await using var factory = Host(); _ = factory.CreateClient();
        var supervisor = factory.Services.GetRequiredService<IActorSupervisor>();
        var producer = factory.Services.GetRequiredService<IActorProducer>();
        await producer.StartAsync(new(ActorType.Realtime, "OrderCompositionVerification"));
        try
        {
            await factory.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            var c = await CompositionFixture.Command(variant, atUtc: DateTime.UtcNow, integrationTiming: true);
            var response = await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand, OrderCompositionExecutionId,
                FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>(c.Subject, c, c.EntityId);
            response.Success.Should().BeTrue(response.ErrorMessage);
            response.Value!.IsCompleted.Should().BeTrue(response.Value.Failed?.ErrorMessage);
            var completion = response.Value.Completed!;
            completion.Result.Payload.IsEmpty.Should().BeTrue(); completion.Result.CompositionResult.Should().NotBeNull();
            var stored = await database.TradeDb.GetOrderCompositionInvocationAsync(c.WorkflowId, c.CommandId);
            stored.Should().NotBeNull(); stored!.Result.PayloadSha256.Should().Be(completion.Result.PayloadSha256);
            await database.TradeDb.UpsertOrderCompositionAsync(stored with { EventId = 987 });
            var container = factory.Services.GetRequiredService<SimpleInjector.Container>();
            container.GetInstance<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor.IOrderCompositionFunctionContext>()
                .Should().BeSameAs(container.GetInstance<IFunctionActorContext<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.OrderComposer.Function.Actor.OrderCompositionFunctionActor>>());
            var repository = container.GetInstance<IEventSourceFunctionStateRepository<OrderCompositionFunctionState, ExecuteOrderCompositionPipelineCommand>>();
            var restored = await repository.LoadStateAsync(c);
            restored.IsCompleted.Should().BeTrue(); restored.Matches(c).Should().BeTrue();
            var replay = await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand, OrderCompositionExecutionId,
                FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>(c.Subject, c, c.EntityId);
            replay.Value!.Completed!.Result.PayloadSha256.Should().Be(completion.Result.PayloadSha256);
            var changed = CompositionFixture.Seal(c with { CausationId = Guid.NewGuid() });
            var conflict = await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand, OrderCompositionExecutionId,
                FunctionResult<OrderCompositionFunctionCompletedEvent, OrderCompositionFunctionFailedEvent>>(changed.Subject, changed, changed.EntityId);
            conflict.Value!.Failed!.ReasonCode.Should().Be("OC.CONTRACT.CONFLICTING_DUPLICATE");
            var history = await database.TradeDb.GetOrderCompositionHistoryAsync(1, 1, c.SelectionBinding.RequestedTradeDate, 100);
            history.Items.Count(x => x.InvocationId == c.CommandId).Should().Be(1);
            var queries = new OrderCompositionQueryApi(producer);
            var exact = await queries.GetInvocationAsync(c.WorkflowId, c.CommandId);
            exact.Success.Should().BeTrue(exact.ErrorMessage); exact.Value!.WorkflowAccepted.Should().BeFalse();
            exact.Value.AcceptanceUnknown.Should().BeTrue();
            (await queries.GetResultAsync(c.WorkflowId, c.CommandId, completion.Id)).Value!.ResultId.Should().Be(completion.Id);
            (await queries.GetResultAsync(c.WorkflowId, c.CommandId, Guid.NewGuid())).Success.Should().BeFalse();
            (await queries.GetHistoryAsync(1, 2, c.SelectionBinding.RequestedTradeDate, 1)).Success.Should().BeFalse("each request requires exact Fund authority");
            var page = await queries.GetHistoryAsync(1, 1, c.SelectionBinding.RequestedTradeDate, 1);
            page.Success.Should().BeTrue(page.ErrorMessage); page.Value!.Items.Should().ContainSingle();
            if (page.Value.PagingState is { } cursor)
                (await queries.GetHistoryAsync(1, 1, c.SelectionBinding.RequestedTradeDate.AddDays(1), 1, cursor)).Success.Should().BeFalse();
        }
        finally { await supervisor.ShutdownAsync(); await producer.StopAsync(); }
    }
    [Fact,Trait("Gate","OC-05"),Trait("Gate","OC-08")]
    public async Task OrderComposer_real_Scylla_orphan_after_append_failure_is_recovered_by_the_identical_Function_request()
    {
        var recorder=new CompositionFailingRepository();
        await using var factory=Host(services=>
        {
            var container=(SimpleInjector.Container)services.Single(x=>x.ServiceType==typeof(SimpleInjector.Container)).ImplementationInstance!;
            container.Register<OrderCompositionFunctionStateRepository>();recorder.Resolve=()=>container.GetInstance<OrderCompositionFunctionStateRepository>();
            container.RegisterInstance<IEventSourceFunctionStateRepository<OrderCompositionFunctionState,ExecuteOrderCompositionPipelineCommand>>(recorder);
        });_=factory.CreateClient();var supervisor=factory.Services.GetRequiredService<IActorSupervisor>();
        var producer=factory.Services.GetRequiredService<IActorProducer>();await producer.StartAsync(new(ActorType.Realtime,"SelectionCrashVerification"));
        try
        {
            await factory.Services.GetRequiredService<TradeSchemaDb>().CreateAllAsync();
            var c=await CompositionFixture.Command(atUtc:DateTime.UtcNow,integrationTiming:true);
            var first=await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand,OrderCompositionExecutionId,FunctionResult<OrderCompositionFunctionCompletedEvent,OrderCompositionFunctionFailedEvent>>(c.Subject,c,c.EntityId);
            first.Value!.IsFailed.Should().BeTrue();first.Value.Failed!.ReasonCode.Should().Be("OC.PERSISTENCE.FAILED");
            (await database.TradeDb.GetOrderCompositionInvocationAsync(c.WorkflowId,c.CommandId)).Should().NotBeNull("projection precedes completed append");
            (await recorder.Resolve().LoadStateAsync(c)).IsCompleted.Should().BeFalse();
            recorder.Fail=false;
            var retry=await producer.RequestFunctionAsync<ExecuteOrderCompositionPipelineCommand,OrderCompositionExecutionId,FunctionResult<OrderCompositionFunctionCompletedEvent,OrderCompositionFunctionFailedEvent>>(c.Subject,c,c.EntityId);
            retry.Value!.IsCompleted.Should().BeTrue(retry.Value.Failed?.ErrorMessage);(await recorder.Resolve().LoadStateAsync(c)).IsCompleted.Should().BeTrue();
        }
        finally{await supervisor.ShutdownAsync();await producer.StopAsync();}
    }
    sealed class CompositionFailingRepository:IEventSourceFunctionStateRepository<OrderCompositionFunctionState,ExecuteOrderCompositionPipelineCommand>
    {
        public bool Fail=true;public Func<OrderCompositionFunctionStateRepository> Resolve=null!;
        public ValueTask<OrderCompositionFunctionState> LoadStateAsync(ExecuteOrderCompositionPipelineCommand c,CancellationToken t=default)=>Resolve().LoadStateAsync(c,t);
        public ValueTask SaveCompletedStateAsync(IFunctionActorContext context,OrderCompositionFunctionState state,ExecuteOrderCompositionPipelineCommand c,CancellationToken t=default)
            =>Fail?ValueTask.FromException(new InvalidOperationException("Injected completed append failure")):Resolve().SaveCompletedStateAsync(context,state,c,t);
    }
}
