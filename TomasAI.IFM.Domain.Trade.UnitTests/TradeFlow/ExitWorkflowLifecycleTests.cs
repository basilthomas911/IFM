using System.Buffers;
using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradePlanDb;
using TomasAI.IFM.Domain.Trade.Futures.Position.Workflow.Command.State;
using TomasAI.IFM.Domain.Trade.Model.Position.Workflow.Model;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.UnitTests.TradeFlow;

public sealed class ExitWorkflowLifecycleTests
{
    [Fact]
    public void Historical_start_preserves_wire_keys_and_rehydrates_a_complete_creation_snapshot()
    {
        var started = Started() with { LifecycleVersion = 1 };
        var reader = new MessagePackReader(MessagePackSerializer.Serialize(started));
        reader.ReadArrayHeader().Should().Be(12);
        var buffer = new ArrayBufferWriter<byte>();
        var writer = new MessagePackWriter(buffer);
        writer.WriteArrayHeader(11);
        for (var index = 0; index < 11; index++) writer.WriteRaw(reader.ReadRaw());
        writer.Flush();

        var legacy = MessagePackSerializer.Deserialize<ExitPositionWorkflowStartedEvent>(buffer.WrittenMemory);
        legacy.LifecycleVersion.Should().Be(0);
        var state = new FuturesExitPositionWorkflowCommandState();
        state.ReplayEvents([legacy]);
        state.Snapshot!.Projection.Should().BeEquivalentTo(ExitWorkflowSnapshotFacts.From(started));
        state.CurrentSourceEventId.Should().Be(started.Id);
        state.Events.Should().BeEmpty();
    }

    [Fact]
    public void Failed_workflow_replays_from_its_own_full_state_and_rejects_a_conflicting_retry()
    {
        var started = Started();
        var state = new FuturesExitPositionWorkflowCommandState();
        state.ReplayEvents([started]);
        var failed = ExitWorkflowContinuation.ForSource(new FailExitPositionWorkflowCommand
            { ReasonCode = "EXIT.COMPOSITION.INVALID_PLAN", Detail = "Plan no longer authorizes a close." },
            FailExitPositionWorkflowCommand.Verb, started);

        failed.Execute(state).Success.Should().BeTrue();
        var source = state.Events.Should().ContainSingle().Which.Should().BeOfType<ExitPositionWorkflowTerminatedEvent>().Subject;
        var restored = new FuturesExitPositionWorkflowCommandState();
        restored.ReplayEvents([MessagePackSerializer.Deserialize<ExitPositionWorkflowTerminatedEvent>(MessagePackSerializer.Serialize(source))]);
        restored.Snapshot!.Projection.State.Should().Be(ExitPositionWorkflowState.Failed);
        restored.Snapshot.FailureDetail.Should().Be(failed.Detail);
        failed.Execute(restored).Success.Should().BeTrue();
        (failed with { Detail = "Different rejection" }).Execute(restored).Success.Should().BeFalse();
        restored.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Read_model_rebuild_never_requests_a_function_or_dispatches_an_order()
    {
        var store = Substitute.For<ITradePlanDbContext>();
        var producer = Substitute.For<IActorProducer>();
        var transactions = Substitute.For<IPostgresEventTransaction>();
        var descriptor = ExitWorkflowProjectionDescriptors.Create(store, () => producer, transactions)
            .Single(item => item.SourceEventType == typeof(ExitPositionWorkflowStartedEvent));
        var started = Started();

        await descriptor.RebuildReadModelAsync!(started, CancellationToken.None);

        await store.Received(1).ProjectExitWorkflowAsync(
            Arg.Is<ExitPositionWorkflowProjection>(row => row.WorkflowId == started.EntityId && row.State == ExitPositionWorkflowState.Started),
            CancellationToken.None);
        producer.ReceivedCalls().Should().BeEmpty();
        transactions.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Lost_function_reply_leaves_the_same_durable_handoff_identity_for_retry()
    {
        var started = Started() with { LifecycleVersion = 1 };
        var producer = Substitute.For<IActorProducer>();
        var requests = new List<ComposeExitOrderCommand>();
        var receipt = new ExitOrderCompositionCompletedEvent { Id = Guid.NewGuid(), EntityId = started.EntityId };
        producer.RequestFunctionAsync<ComposeExitOrderCommand, ExitPositionWorkflowId,
            FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>>(
                Arg.Any<ActorSubject>(), Arg.Any<ComposeExitOrderCommand>(), started.EntityId, CancellationToken.None)
            .Returns(call =>
            {
                requests.Add(call.Arg<ComposeExitOrderCommand>());
                return requests.Count == 1
                    ? new ServiceFailed<FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>>(503, "Reply lost")
                    : new ServiceOk<FunctionResult<ExitOrderCompositionCompletedEvent, ExitPositionWorkflowFailedEvent>>(new(receipt, null));
            });
        producer.RequestAsync<RecordExitOrderCompositionCommand, ExitPositionWorkflowId, GuidResult>(
            Arg.Any<ActorSubject>(), Arg.Any<RecordExitOrderCompositionCommand>(), started.EntityId, CancellationToken.None)
            .Returns(new ServiceOk<GuidResult>(new(Guid.NewGuid())));

        var first = () => ExitWorkflowContinuation.ApplyAsync(started, producer, CancellationToken.None);
        await first.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Reply lost");
        await producer.DidNotReceive().RequestAsync<RecordExitOrderCompositionCommand, ExitPositionWorkflowId, GuidResult>(
            Arg.Any<ActorSubject>(), Arg.Any<RecordExitOrderCompositionCommand>(), started.EntityId, CancellationToken.None);

        await ExitWorkflowContinuation.ApplyAsync(started, producer, CancellationToken.None);

        requests.Should().HaveCount(2);
        requests[1].Should().BeEquivalentTo(requests[0]);
        await producer.Received(1).RequestAsync<RecordExitOrderCompositionCommand, ExitPositionWorkflowId, GuidResult>(
            Arg.Any<ActorSubject>(), Arg.Is<RecordExitOrderCompositionCommand>(command =>
                command.PredecessorEventId == started.Id && command.ExpectedRevision == 1 && command.Receipt.Id == receipt.Id),
            started.EntityId, CancellationToken.None);
    }

    static ExitPositionWorkflowStartedEvent Started()
    {
        var date = new DateOnly(2026, 9, 12);
        var position = new StrategyPositionSnapshot
        { Id = new(new TradeEntityId(1, 2, 3, 4), Guid.NewGuid()), StrategyKind = TradeStrategyKind.FuturesOutright };
        return new()
        {
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid(), EntityId = new(position.Id, date, Guid.NewGuid()),
            StrategyKind = position.StrategyKind, SourcePlanEventId = Guid.NewGuid(),
            ReceivedOn = new DateTime(2026, 9, 12, 15, 0, 0, DateTimeKind.Utc),
            ExitPlan = new() { Position = position, ValueDate = date, State = TradePlanState.ExitRequired,
                Action = TradePlanAction.ExitAtMarket, RequiresExit = true }
        };
    }
}
