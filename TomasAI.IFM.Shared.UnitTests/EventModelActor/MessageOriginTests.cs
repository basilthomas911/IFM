using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Time;
using Xunit;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class MessageOriginTests
{
    [Fact]
    public void CapturePreservesCreationAcrossClockMovementAndRetry()
    {
        var clock = new TestClock();
        var message = MessageOrigins.Create(clock, () => new object(), "test-business");
        var initial = MessageOrigins.Get(message);
        clock.Now = clock.Now.AddDays(2);
        var retried = MessageOrigins.Capture(message, clock, "test-business");
        Assert.Same(initial, retried);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero), retried.OriginatedAtUtc);
    }

    [Fact]
    public void ConcreteCommandOriginIsCapturedRatherThanComputedOrRecreatedOnDecode()
    {
        var command = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands.ExecuteIntrinsicTimeStrategyWorkflowCommand();
        var clock = new TestClock();
        Assert.Equal(default, command.OriginatedOn);
        var origin = MessageOrigins.Capture(command, clock, "test-business");
        clock.Now = clock.Now.AddYears(1);
        Assert.Equal(origin.OriginatedAtUtc.UtcDateTime, command.OriginatedOn);
        var bytes = MessagePack.MessagePackSerializer.Serialize(command);
        var decoded = MessagePack.MessagePackSerializer.Deserialize<
            TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands.ExecuteIntrinsicTimeStrategyWorkflowCommand>(bytes);
        Assert.Equal(default, decoded.OriginatedOn);
        MessageOrigins.Restore(decoded, origin);
        Assert.Equal(command.OriginatedOn, decoded.OriginatedOn);
        var forwarded = MessageOrigins.Copy(command, command with { PostEvents = false });
        Assert.Equal(origin, MessageOrigins.Get(forwarded));
    }

    [Fact]
    public void CaptureFillsOnlyMissingEventReceiptAndKeepsSourceFacts()
    {
        var clock = new TestClock();
        var value = new TomasAI.IFM.Shared.EventSourcing.DomainEvent();
        var origin = MessageOrigins.Capture(value, clock);
        Assert.Equal(origin.OriginatedAtUtc.UtcDateTime, value.ReceivedOn);
        var supplied = new TomasAI.IFM.Shared.EventSourcing.DomainEvent
        { ReceivedOn = clock.Now.UtcDateTime.AddHours(-1) };
        MessageOrigins.Capture(supplied, clock);
        Assert.Equal(clock.Now.UtcDateTime.AddHours(-1), supplied.ReceivedOn);
    }

    [Fact]
    public void ComputedErrorDateIsNotAssignedOrInventedAsASeparateSourceFact()
    {
        var source = new TomasAI.IFM.Domain.Portfolio.Shared.Financial.LedgerPostingFailedEvent();
        var origin = MessageOrigins.Capture(source, new TestClock());
        Assert.Equal(origin.OriginatedAtUtc.UtcDateTime, source.ReceivedOn);
        Assert.Equal(default, source.FailedAtUtc);
        Assert.Equal(default, source.ErrorDate);
    }

    [Fact]
    public void NewDerivedMessageHasItsOwnTimeAndKeepsCausation()
    {
        var clock = new TestClock();
        var parent = MessageOrigins.Create(clock, () => new object());
        clock.Now = clock.Now.AddSeconds(1);
        var id = Guid.NewGuid();
        var child = MessageOrigins.Create(clock, () => new object(), causationId: id);
        Assert.NotEqual(MessageOrigins.Get(parent)!.OriginatedAtUtc, MessageOrigins.Get(child)!.OriginatedAtUtc);
        Assert.Equal(id, MessageOrigins.Get(child)!.CausationId);
    }

    [Fact]
    public void UnknownLegacyOriginStaysUnknownAndRestoreIsImmutable()
    {
        var value = new object();
        Assert.Null(MessageOrigins.Get(value));
        Assert.Same(value, MessageOrigins.Restore(value, null));
        Assert.Null(MessageOrigins.Get(value));
        Assert.Null(MessageOrigins.ForPublication(value, new TestClock()));
        Assert.Throws<InvalidOperationException>(() => MessageOrigins.Capture(value, new TestClock()));
        value = new object();
        var origin = new MessageOrigin(new TestClock().Now, "stored");
        MessageOrigins.Restore(value, origin);
        MessageOrigins.Restore(value, origin);
        Assert.Throws<InvalidOperationException>(() => MessageOrigins.Restore(value,
            new MessageOrigin(origin.OriginatedAtUtc.AddSeconds(1), "stored")));
    }

    [Fact]
    public async Task ConcurrentCaptureHasOneAuthoritativeOrigin()
    {
        var message = new object();
        var clock = new TestClock();
        var captures = await Task.WhenAll(Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => MessageOrigins.Capture(message, clock))));
        Assert.All(captures, origin => Assert.Same(captures[0], origin));
    }

    [Fact]
    public void OriginRejectsInvalidUtcAndScope()
    {
        Assert.Throws<ArgumentException>(() => new MessageOrigin(default, "system"));
        Assert.Throws<ArgumentException>(() => new MessageOrigin(DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(1)), "system"));
        Assert.Throws<ArgumentException>(() => new MessageOrigin(new TestClock().Now, ""));
        Assert.Throws<ArgumentException>(() => new MessageOrigin(new TestClock().Now, "system", runId: Guid.Empty));
    }

    [Fact]
    public void PhysicalClockDefaultsToSystemAndDoesNotResolveBusinessClock()
    {
        Assert.Same(TimeProvider.System, new PhysicalTime().Provider);
        var clock = new TestClock();
        Assert.Same(clock, PhysicalTime.FromProvider(clock).Provider);
        var policy = new FunctionExecutionPolicy(clock, clock.Now.UtcDateTime.AddSeconds(10));
        Assert.Same(clock, policy.Clock);
        Assert.Same(TimeProvider.System, policy.PhysicalClock);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FunctionExecutionPolicy(clock, null, physicalTimeout: TimeSpan.Zero));
    }

    [Fact]
    public void ConfiguredClockIdentityFlowsWithoutGlobalStateOrOverwritingExplicitScope()
    {
        var clock = new ScopedClock();
        var origin = MessageOrigins.Capture(new object(), clock);
        Assert.Equal(clock.ClockDomainId, origin.ClockDomainId);
        Assert.Equal(clock.RunId, origin.RunId);
        Assert.Equal(clock.Generation, origin.Generation);
        var explicitOrigin = MessageOrigins.Capture(new object(), clock, "administrative");
        Assert.Equal("administrative", explicitOrigin.ClockDomainId);
        Assert.Equal("system", MessageOrigins.Capture(new object(), TimeProvider.System).ClockDomainId);
    }

    sealed class ScopedClock : TimeProvider, IMessageClockIdentity
    {
        public string ClockDomainId => "controlled-test";
        public Guid? RunId { get; } = Guid.NewGuid();
        public Guid? Generation { get; } = Guid.NewGuid();
        public override DateTimeOffset GetUtcNow() => new(2025, 11, 3, 12, 0, 0, TimeSpan.Zero);
    }

    [Fact]
    public async Task FrozenLogicalTimersCannotDisablePhysicalIoDeadline()
    {
        var logical = new NonTickingClock();
        using var deadline = new ClockDeadline(TimeSpan.FromMilliseconds(100), logical, TimeProvider.System);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Task.Delay(Timeout.InfiniteTimeSpan, deadline.Token).WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(deadline.IsCancellationRequested);
        Assert.Equal(new DateTimeOffset(2025, 11, 3, 12, 0, 0, TimeSpan.Zero), logical.GetUtcNow());
    }

    sealed class NonTickingClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2025, 11, 3, 12, 0, 0, TimeSpan.Zero);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => new InactiveTimer();
        sealed class InactiveTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Fact]
    public void StateFactoryUsesBusinessClockBeforeReducerAndNeverRestampsReplay()
    {
        var clock = new TestClock();
        var state = new ClockState();
        var resolver = new ClockStateResolver(state);
        var factory = new TomasAI.IFM.Application.EventModelActor.EventSourceActorStateFactory(resolver, clock);
        factory.CreateState<ClockState>();
        var created = new TomasAI.IFM.Shared.EventSourcing.DomainEvent();
        Assert.True(state.Update(created));
        Assert.Equal(clock.Now.UtcDateTime, state.LastReceived);
        Assert.Equal(clock.Now, MessageOrigins.Get(created)!.OriginatedAtUtc);
        clock.Now = clock.Now.AddDays(1);
        var legacy = new TomasAI.IFM.Shared.EventSourcing.DomainEvent();
        state.ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.IEvent[] { legacy });
        Assert.Equal(default, state.LastReceived);
        Assert.Null(MessageOrigins.Get(legacy));
        var restored = new TomasAI.IFM.Shared.EventSourcing.DomainEvent { ReceivedOn = created.ReceivedOn };
        MessageOrigins.Restore(restored, MessageOrigins.Get(created));
        state.ReplayEvents(new TomasAI.IFM.Shared.EventSourcing.IEvent[] { restored });
        Assert.Equal(created.ReceivedOn, state.LastReceived);
        Assert.Equal(MessageOrigins.Get(created), MessageOrigins.Get(restored));
    }

    sealed class ClockStateResolver(ClockState state) : TomasAI.IFM.Shared.EventModelActor.Contracts.IActorStateFactoryResolver
    {
        public object Resolve(Type type) => state;
    }

    sealed class ClockState : TomasAI.IFM.Application.EventModelActor.BaseEventSourceActorState<ClockState>
    {
        public override TomasAI.IFM.Shared.EventModelActor.ActorThreadId Id { get; set; }
        public DateTime LastReceived { get; private set; }
        protected override bool Apply(TomasAI.IFM.Shared.EventSourcing.IEvent source)
        {
            LastReceived = source.ReceivedOn;
            return true;
        }
    }

    [Fact]
    public void FailureConversionDefersItsOwnErrorTimeAndPreservesTheSourceReceipt()
    {
        var clock = new TestClock();
        var source = new TomasAI.IFM.Domain.Trade.Shared.Events.OptionTradePositionClosedEvent
        {
            Subject = new ActorSubject(ActorType.Event, "Trade", "Closed", "1.2"),
            EntityId = new TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId(1, 2),
            ReceivedOn = clock.Now.UtcDateTime.AddDays(-1),
            Id = Guid.NewGuid(), CommandId = Guid.NewGuid()
        };
        var failure = source.ToFailEvent<TomasAI.IFM.Domain.Trade.Shared.Events.OptionTradePositionClosedFailEvent,
            TomasAI.IFM.Domain.Trade.Shared.OptionTradeEntityId>(new InvalidOperationException("fixture"));
        Assert.Equal(default, failure.ErrorDate);
        Assert.Equal(source.ReceivedOn, failure.ReceivedOn);
        var origin = MessageOrigins.Capture(failure, clock);
        Assert.Equal(clock.Now.UtcDateTime, failure.ErrorDate);
        Assert.Equal(origin.OriginatedAtUtc.UtcDateTime, failure.ErrorDate);
        Assert.Equal(source.ReceivedOn, failure.ReceivedOn);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProducerPreparationPreservesExistingIdentityButNewCommandsHaveTheirOwnOrigin(bool legacy)
    {
        var clock = new TestClock();
        var source = new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands.ExecuteIntrinsicTimeStrategyWorkflowCommand
        { CommandId = Guid.NewGuid() };
        var origin = legacy ? null : MessageOrigins.Capture(source, clock);
        if (legacy) MessageOrigins.Restore(source, null);
        clock.Now = clock.Now.AddDays(1);
        var forwarded = MessageOrigins.CopyCommandIfSameIdentity(source, source with { PostEvents = false });
        Assert.Equal(origin, MessageOrigins.ForPublication(forwarded, clock));
        var derived = MessageOrigins.CopyCommandIfSameIdentity(source, source with { CommandId = Guid.NewGuid() });
        Assert.Null(MessageOrigins.Get(derived));
        Assert.Equal(clock.Now, MessageOrigins.Capture(derived, clock).OriginatedAtUtc);
    }

    sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
