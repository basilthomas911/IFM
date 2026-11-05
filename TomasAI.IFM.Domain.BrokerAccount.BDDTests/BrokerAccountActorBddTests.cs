using TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.EventProjector;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Command;
using TomasAI.IFM.Domain.BrokerAccount.Command.EventProjector;
using TomasAI.IFM.Domain.BrokerAccount.Command.State;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Event;
using TomasAI.IFM.Domain.BrokerAccount.Event.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Query;
using TomasAI.IFM.Domain.BrokerAccount.Query.Actor;
using TomasAI.IFM.Domain.BrokerAccount.Query.Model;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Domain.BrokerAccount.BDDTests;

public sealed class BrokerAccountActorBddTests(EventSourceActorSnapshotRangeFixture fixture)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task Given_a_redelivered_observation_when_forwarded_then_the_command_identity_is_preserved()
    {
        var account = new BrokerAccountId("EMU");
        var observed = new BrokerAccountSnapshotObservedEvent
        {
            Id = Guid.NewGuid(),
            EntityId = account,
            Environment = BrokerEnvironment.Emulator,
            Snapshot = new BrokerAccountSnapshotEvidence
            {
                AccountAlias = account.AccountAlias,
                Currency = "USD",
                Generation = 1,
                AsOfUtc = DateTime.UtcNow
            }
        };
        var actorService = Substitute.For<IActorService>();
        var context = Substitute.For<IBrokerAccountEventContext>();
        context.ActorService.Returns(actorService);
        var commandId = BrokerAccountSnapshotIdentity.Create(observed.Environment, observed.Snapshot);
        actorService.SendAsync<RecordBrokerAccountSnapshotCommand, BrokerAccountId>(
            Arg.Any<RecordBrokerAccountSnapshotCommand>(), account)
            .Returns(ValueTask.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(commandId)),
                ValueTask.FromResult<ServiceResult<Guid>>(new ServiceFailed<Guid>(25104, "rejected")));

        await observed.ExecuteAsync(context, NullLogger<BrokerAccountEventActor>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            observed.ExecuteAsync(context, NullLogger<BrokerAccountEventActor>.Instance).AsTask());

        await actorService.Received(2).SendAsync<RecordBrokerAccountSnapshotCommand, BrokerAccountId>(
            Arg.Is<RecordBrokerAccountSnapshotCommand>(command => command.CommandId == commandId), account);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_a_projected_account_query_when_dispatched_then_the_actor_replies_once(bool found)
    {
        var account = new BrokerAccountId("EMU");
        var definition = found ? new BrokerAccountDefinition { Id = account, Revision = 7 } : null;
        var context = Substitute.For<IBrokerAccountQueryContext>();
        var store = Substitute.For<IBrokerAccountReadStore>();
        context.Logger.Returns(NullLogger<BrokerAccountQueryActor>.Instance);
        context.Store.Returns(store);
        store.GetAsync(account, CancellationToken.None).Returns(ValueTask.FromResult(definition));
        var query = new GetBrokerAccountQuery
        {
            Subject = new(ActorType.Query, GetBrokerAccountQuery.Actor, GetBrokerAccountQuery.Verb, account.Format()),
            BrokerAccountId = account
        };

        IQueryActor<BrokerAccountQueryActor> actor = new BrokerAccountQueryActor(context);
        await actor.ReceiveAsync(context, query);

        await context.Received(1).ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            Arg.Is<ServiceResult<BrokerAccountDefinition?>>(result => result.Success && result.Value == definition));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query.ExecuteAsync(context, cancellation.Token).AsTask());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Given_a_resynchronization_intent_when_replayed_then_the_broker_refresh_is_attempted_at_most_once(
        bool matchingAccount, bool interrupted)
    {
        var account = new BrokerAccountId("EMU");
        var command = new RequestBrokerAccountResynchronizationCommand
        {
            EntityId = account,
            CommandId = Guid.NewGuid(),
            Subject = new(ActorType.Command, BrokerAccountActorNames.Command, RequestBrokerAccountResynchronizationCommand.Verb, account.Format()),
            RequestedAtUtc = DateTime.UtcNow.AddSeconds(-1)
        };
        var state = new BrokerAccountCommandState();
        Assert.True(command.Execute(state).Success);
        Assert.Null(state.BrokerAccountDefinition);
        var request = Assert.IsType<BrokerAccountResynchronizationRequestedEvent>(Assert.Single(state.Events));

        var broker = Substitute.For<ITradeBroker>();
        var service = Substitute.For<IActorService>();
        broker.AccountAlias.Returns(matchingAccount ? account.AccountAlias : "OTHER");
        broker.Environment.Returns(BrokerEnvironment.Emulator);
        var snapshot = new BrokerAccountSnapshot(account.AccountAlias, "USD", 10m, 10m, true, true, 1, DateTime.UtcNow, []);
        broker.ResynchronizeAccountAsync(Arg.Any<CancellationToken>()).Returns(_ => interrupted
            ? ValueTask.FromException<BrokerAccountSnapshot>(new OperationCanceledException())
            : ValueTask.FromResult(snapshot));
        service.SendAsync<RecordBrokerAccountSnapshotCommand, BrokerAccountId>(
            Arg.Any<RecordBrokerAccountSnapshotCommand>(), account, Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<ServiceResult<Guid>>(new ServiceOk<Guid>(Guid.NewGuid())));

        var projector = CreateProjector(broker, service);
        var descriptor = Assert.Single(projector.ProjectionDescriptors, candidate => candidate.SourceEventType == request.GetType());
        var context = new ProjectionExecutionContext("test", 1, 1,
            new EventProjectorEffectIdentity("test", 1, EventProjectorEffectKind.TargetProjection),
            Guid.NewGuid(), EventProjectionIdempotencyStrategy.NaturalKeyMutation, CancellationToken.None, 1);
        var result = await descriptor.ApplyAsync(request, context);

        var replay = await descriptor.ApplyAsync(request, context);
        Assert.Equal(matchingAccount && !interrupted, replay.Success);
        Assert.Equal(matchingAccount && !interrupted, result.Success);
        if (interrupted)
            Assert.Equal("BrokerAccount.RESYNC.OUTCOME_UNKNOWN", replay.ErrorMessage);
        await broker.Received(matchingAccount ? 1 : 0).ResynchronizeAccountAsync(Arg.Any<CancellationToken>());
        await service.Received(matchingAccount && !interrupted ? 1 : 0).SendAsync<RecordBrokerAccountSnapshotCommand, BrokerAccountId>(
            Arg.Is<RecordBrokerAccountSnapshotCommand>(prepared => prepared.EntityId == account &&
                prepared.Snapshot.AccountAlias == account.AccountAlias && prepared.Snapshot.AsOfUtc == snapshot.AsOfUtc),
            account, Arg.Any<CancellationToken>());
        Assert.IsType<BrokerAccountResynchronizedCompleteEvent>(descriptor.CompletedEventFactory(request));
        Assert.IsType<BrokerAccountResynchronizedFailEvent>(descriptor.FailedEventFactory(request, new InvalidOperationException("broker failed")));
    }

    [Theory]
    [InlineData(BrokerAccountOperation.RecordBrokerAccountSnapshot, typeof(BrokerAccountSnapshotRecordedCompleteEvent), typeof(BrokerAccountSnapshotRecordedFailEvent))]
    [InlineData(BrokerAccountOperation.SubmitAccountQualificationEvidence, typeof(AccountQualificationEvidenceSubmittedCompleteEvent), typeof(AccountQualificationEvidenceSubmittedFailEvent))]
    [InlineData(BrokerAccountOperation.AcceptAccountQualification, typeof(AccountQualificationAcceptedCompleteEvent), typeof(AccountQualificationAcceptedFailEvent))]
    [InlineData(BrokerAccountOperation.RevokeAccountQualification, typeof(AccountQualificationRevokedCompleteEvent), typeof(AccountQualificationRevokedFailEvent))]
    [InlineData(BrokerAccountOperation.SetManualTradingHold, typeof(ManualTradingHoldSetCompleteEvent), typeof(ManualTradingHoldSetFailEvent))]
    [InlineData(BrokerAccountOperation.ReleaseManualTradingHold, typeof(ManualTradingHoldReleasedCompleteEvent), typeof(ManualTradingHoldReleasedFailEvent))]
    [InlineData(BrokerAccountOperation.RequestBrokerAccountResynchronization, typeof(BrokerAccountResynchronizedCompleteEvent), typeof(BrokerAccountResynchronizedFailEvent))]
    public void Given_a_committed_operation_when_projected_then_outcomes_keep_the_concrete_contract_and_correlation(
        BrokerAccountOperation operation, Type completeType, Type failType)
    {
        var projector = CreateProjector(Substitute.For<ITradeBroker>(), Substitute.For<IActorService>());
        var source = new BrokerAccountOperationDecidedEvent
        {
            EntityId = new("EMU"),
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EventId = 9,
            Operation = operation,
            Accepted = false,
            RejectionCode = 25104,
            RejectionReason = "original business rejection"
        };
        var descriptor = Assert.Single(projector.ProjectionDescriptors, candidate => candidate.SourceEventType == source.GetType());
        var completed = Assert.IsAssignableFrom<BrokerAccountOperationComplete>(
            descriptor.CompletedEventFactory(source));
        var failed = Assert.IsAssignableFrom<BrokerAccountOperationFail>(
            descriptor.FailedEventFactory(source, new InvalidOperationException("secondary error")));
        Assert.IsType(completeType, completed);
        Assert.IsType(failType, failed);
        Assert.Equal(source.CommandId, completed.CommandId);
        Assert.Equal(source.EventId, completed.EventId);
        Assert.Equal(source.CommandId, failed.CommandId);
        Assert.Equal(source.RejectionCode, failed.ErrorCode);
        Assert.Equal(source.RejectionReason, failed.ErrorMessage);
        Assert.Equal(completeType.Name[..^"Event".Length], completed.Subject.Verb);
        Assert.Equal(failType.Name[..^"Event".Length], failed.Subject.Verb);
    }

    private BrokerAccountEventProjector CreateProjector(ITradeBroker broker, IActorService service) =>
        new(Substitute.For<IBrokerAccountProjectionWriter>(), Substitute.For<IDurableReplayQueue>(),
            fixture.ActorEventDb, Substitute.For<IBlackboardService>(), NullLogger<BrokerAccountEventProjector>.Instance,
            broker, service);

    [Fact]
    public async Task Given_a_committed_hold_when_projector_restarts_then_it_rebuilds_without_republishing_the_outcome()
    {
        var account = new BrokerAccountId($"test-{Guid.NewGuid():N}");
        var source = new BrokerAccountOperationAppliedEvent
        {
            Id = Guid.NewGuid(),
            CommandId = Guid.NewGuid(),
            EntityId = account,
            Subject = new(ActorType.Event, BrokerAccountActorNames.Event, BrokerAccountOperationAppliedEvent.Verb, account.Format()),
            Operation = BrokerAccountOperation.SetManualTradingHold,
            BrokerAccountDefinition = new BrokerAccountDefinition { Id = account, ManualHold = true, Revision = 1 }
        };
        var stream = new ActorSubject(ActorType.Command, BrokerAccountActorNames.Command, SetManualTradingHoldCommand.Verb, account.Format()).StreamId;
        var committed = Assert.Single(await fixture.ActorEventDb.SaveEventsAsync(stream, source.CommandId,
            new DomainEventCollection([source]), 0, CancellationToken.None));
        var writer = Substitute.For<IBrokerAccountProjectionWriter>();
        var actor = Substitute.For<ICommandActorContext>();
        var published = new TaskCompletionSource<ManualTradingHoldSetCompleteEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        actor.SendAsync<ManualTradingHoldSetCompleteEvent, BrokerAccountId>(
            Arg.Any<ManualTradingHoldSetCompleteEvent>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            published.TrySetResult(call.Arg<ManualTradingHoldSetCompleteEvent>());
            return ValueTask.CompletedTask;
        });
        var options = new EventProjectorReliabilityOptions
        {
            FencedExecutionEnabled = true,
            TransactionalOutboxEnabled = true,
            OutboxPollingInterval = TimeSpan.FromMilliseconds(50)
        };
        BrokerAccountEventProjector StartableProjector() => new(writer, Substitute.For<IDurableReplayQueue>(),
            fixture.ActorEventDb, fixture.Blackboard, NullLogger<BrokerAccountEventProjector>.Instance,
            Substitute.For<ITradeBroker>(), Substitute.For<IActorService>(), options);

        await using (var initial = StartableProjector())
        {
            await initial.StartAsync(actor);
            await initial.ProcessDomainEventAsync(committed);
            var outcome = await published.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(source.CommandId, outcome.CommandId);
            Assert.Equal(committed.EventId, outcome.EventId);
            await writer.Received(1).ProjectAsync(source.BrokerAccountDefinition, Arg.Any<CancellationToken>());
            await WaitForPublishedOutboxAsync(committed.EventId);
        }

        await using var restarted = StartableProjector();
        await restarted.StartAsync(actor);
        await restarted.ProcessDomainEventAsync(committed);
        var rebuilder = new ReadProjectionRebuilder(restarted.ProjectionDescriptors);
        Assert.Equal(1, await rebuilder.RebuildAsync(new[] { committed }.ToAsyncEnumerable()));

        await writer.Received(2).ProjectAsync(source.BrokerAccountDefinition, Arg.Any<CancellationToken>());
        await actor.Received(1).SendAsync<ManualTradingHoldSetCompleteEvent, BrokerAccountId>(
            Arg.Any<ManualTradingHoldSetCompleteEvent>(), Arg.Any<CancellationToken>());
    }

    private async Task WaitForPublishedOutboxAsync(long eventId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var query = new NpgsqlCommand("""
            SELECT EXISTS (
                SELECT 1 FROM event_projector_outbox
                WHERE ProjectorName = $1 AND EventId = $2 AND Status = 'Published'
            )
            """, connection);
        query.Parameters.AddWithValue(nameof(BrokerAccountEventProjector));
        query.Parameters.AddWithValue(eventId);
        while (await query.ExecuteScalarAsync(timeout.Token) is not true)
            await Task.Delay(TimeSpan.FromMilliseconds(25), timeout.Token);
    }
}
