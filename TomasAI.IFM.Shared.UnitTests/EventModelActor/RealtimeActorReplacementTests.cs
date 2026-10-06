using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.Analytics.ResetTests;

public sealed class RealtimeActorReplacementTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ReplacementDiscardsQueueAndProcessesNewWorkBeforeOldHandlerExits(int implementation)
    {
        var container = new Mock<IContainerInstance>();
        container.Setup(value => value.Resolve<IActorThreadQueue>()).Returns(() => implementation switch
        {
            1 => new ActorThreadQueueMpscRing(32),
            2 => new ActorThreadQueueSpscRing(32),
            _ => (IActorThreadQueue)new ActorThreadQueueV2(32)
        });
        var factory = new Mock<IActorFactory>();
        container.Setup(value => value.Resolve<IActorFactory>()).Returns(factory.Object);
        await using var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
        var old = new TestActor(true);
        var replacement = new TestActor(false);
        factory.Setup(value => value.CreateRealtimeReplacement(typeof(TestActor))).Returns(replacement);
        supervisor.AddActor(old);
        await old.StartAsync(supervisor);
        var active = new Message();
        var queued = new Message();
        Assert.True(old.Mailbox.ThreadQueues.Write(active, active.Subject));
        await old.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(old.Mailbox.ThreadQueues.Write(queued, queued.Subject));
        try
        {
            await supervisor.RestartAsync(old.Id).AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(old.Release.Task.IsCompleted);
            Assert.Equal(1, queued.Disposals);
            Assert.Equal(0, active.Disposals);
            Assert.NotSame(old.Mailbox, replacement.Mailbox);
            Assert.True(replacement.Mailbox.ThreadQueues.IsAccepting);
            var next = new Message();
            Assert.True(replacement.Mailbox.ThreadQueues.Write(next, next.Subject));
            await replacement.Completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1, replacement.Effects);
            old.Release.SetResult();
            await active.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(0, old.Effects);
            Assert.Equal(1, active.Disposals);
        }
        finally { old.Release.TrySetResult(); }
    }

    [Fact]
    public void RetiredGenerationRejectsMutationAndContextDispatch()
    {
        var supervisor = new Mock<IActorSupervisor>();
        var context = new EventActorContext(supervisor.Object, new(ActorType.Realtime, "Setup"));
        using var execution = context.RealtimeGeneration.Enter();
        context.RealtimeGeneration.Retire();
        Assert.ThrowsAny<OperationCanceledException>(() => RealtimeActorGeneration.EnterMutation());
        Assert.ThrowsAny<OperationCanceledException>(() => RealtimeActorGeneration.ThrowIfRetired());
    }

    [Fact]
    public async Task ClosedAdmissionIsNotReportedHealthyWhenMailboxIsEmpty()
    {
        var container = new Mock<IContainerInstance>();
        await using var supervisor = new ActorSupervisor(container.Object, NullLogger<ActorSupervisor>.Instance);
        var actor = new TestActor(false);
        supervisor.AddActor(actor);
        await actor.StartAsync(supervisor);
        actor.Mailbox.ThreadQueues.PauseAdmission();
        Assert.Equal(SupervisorActorHealthStatus.Yellow,
            supervisor.RuntimeContext.CaptureSnapshot().Actors[0].Status);
    }

    [Fact]
    public void FinancialNamespacesAreExcludedFromDisposableReset()
    {
        Assert.False(RealtimeActorResetPolicy.CanReplace(typeof(TestActor), ActorType.Command));
        Assert.False(RealtimeActorResetPolicy.CanReplace(typeof(ActorSupervisor), ActorType.Realtime));
    }

    sealed class TestActor(bool blocked) : IActor, IReplaceableRealtimeActor
    {
        public ActorMailboxId Id { get; } = new(ActorType.Realtime, "ResetSetup");
        public IActorMailbox Mailbox { get; private set; } = null!;
        public bool IsRunning { get; private set; }
        public bool IsParent => false;
        public RealtimeActorGeneration RealtimeGeneration { get; } = new();
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Effects;
        public void RetireRealtimeGeneration() { RealtimeGeneration.Retire(); IsRunning = false; }
        public ValueTask StartAsync(IActorSupervisor supervisor)
        { Mailbox = supervisor.CreateMailbox(Id); IsRunning = true; return ValueTask.CompletedTask; }
        public ValueTask StopAsync() { IsRunning = false; return ValueTask.CompletedTask; }
        public ValueTask HandleMessageAsync(IActorMessage message) => Process();
        async ValueTask Process()
        {
            using var scope = RealtimeGeneration.Enter();
            Entered.TrySetResult();
            if (blocked) await Release.Task;
            using (RealtimeActorGeneration.EnterMutation()) Effects++;
            Completed.TrySetResult();
        }
    }

    sealed class Message : IActorMessage
    {
        public ActorSubject Subject { get; } = new(ActorType.Realtime, "ResetSetup", "Updated", "ES");
        public ActorSubject ReplySubject { get; set; } = default!;
        public int Disposals;
        public TaskCompletionSource Disposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Dispose() { Interlocked.Increment(ref Disposals); Disposed.TrySetResult(); }
        public void ReleasePayload() { }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => null;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => null;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => null;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public NatsMsg<byte[]> GetMessage() => default;
    }
}
