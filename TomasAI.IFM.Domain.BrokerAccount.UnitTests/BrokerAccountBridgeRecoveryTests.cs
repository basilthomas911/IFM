using Xunit;
using System.Runtime.CompilerServices;
using MessagePack;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.TradeBroker.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Contracts;
using TomasAI.IFM.Domain.BrokerAccount.Realtime;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.UnitTests;
public sealed class BrokerAccountBridgeRecoveryTests
{
    static BrokerAccountSnapshot Snapshot()=>new("EMU","USD",100000,90000,true,true,1,new(2026,11,25,15,0,0,DateTimeKind.Utc),[]){SourceSequence=1};
    static async IAsyncEnumerable<BrokerObservation> Quiet([EnumeratorCancellation] CancellationToken token)
    {await Task.Delay(Timeout.Infinite,token);yield break;}
    static async IAsyncEnumerable<BrokerObservation> Broken([EnumeratorCancellation] CancellationToken token)
    {await Task.Yield();throw new IOException("Reader disconnected.");
#pragma warning disable CS0162
        yield break;
#pragma warning restore CS0162
    }
    [Fact]
    public async Task Last_failed_snapshot_retries_exact_payload_without_another_callback()
    {
        var broker=Substitute.For<ITradeBroker>();broker.AccountAlias.Returns("EMU");broker.Environment.Returns(BrokerEnvironment.Emulator);
        broker.ResynchronizeAccountAsync(Arg.Any<CancellationToken>()).Returns(Snapshot());broker.GetAccountSnapshotAsync(Arg.Any<CancellationToken>()).Returns(Snapshot());
        broker.ObserveAccountAsync(Arg.Any<CancellationToken>()).Returns(c=>Quiet(c.Arg<CancellationToken>()));
        var supervisor=Substitute.For<IActorSupervisor>();supervisor.IsReady.Returns(true);var producer=Substitute.For<IJSActorProducer>();supervisor.GetJSProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var attempts=new List<byte[]>();var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<BrokerAccountSnapshotObservedEvent,BrokerAccountId>(Arg.Any<ActorSubject>(),Arg.Any<BrokerAccountSnapshotObservedEvent>(),Arg.Any<CancellationToken>()).Returns(c=>
        {
            attempts.Add(MessagePackSerializer.Serialize(c.Arg<BrokerAccountSnapshotObservedEvent>()));
            if(attempts.Count<3)return ValueTask.FromException(new IOException("Accepted response unknown."));done.TrySetResult();return ValueTask.CompletedTask;
        });
        await using var bridge=new BrokerAccountObservationBridge(broker,supervisor,NullLogger<BrokerAccountObservationBridge>.Instance,TimeSpan.FromMilliseconds(10));await bridge.StartAsync();
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Equal(attempts[0],attempts[1]);Assert.Equal(attempts[0],attempts[2]);
        await bridge.StopAsync();Assert.False(bridge.IsHealthy);
    }
    [Fact]
    public async Task Initial_resync_and_callback_reader_faults_are_supervised()
    {
        var broker=Substitute.For<ITradeBroker>();broker.AccountAlias.Returns("EMU");broker.Environment.Returns(BrokerEnvironment.Emulator);var resyncs=0;var readers=0;
        broker.ResynchronizeAccountAsync(Arg.Any<CancellationToken>()).Returns(_=>++resyncs==1?ValueTask.FromException<BrokerAccountSnapshot>(new IOException("Startup failed.")):ValueTask.FromResult(Snapshot()));
        broker.GetAccountSnapshotAsync(Arg.Any<CancellationToken>()).Returns(Snapshot());var restarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        broker.ObserveAccountAsync(Arg.Any<CancellationToken>()).Returns(c=>{if(++readers==1)return Broken(c.Arg<CancellationToken>());restarted.TrySetResult();return Quiet(c.Arg<CancellationToken>());});
        var supervisor=Substitute.For<IActorSupervisor>();supervisor.IsReady.Returns(true);var producer=Substitute.For<IJSActorProducer>();supervisor.GetJSProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var published=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<BrokerAccountSnapshotObservedEvent,BrokerAccountId>(Arg.Any<ActorSubject>(),Arg.Any<BrokerAccountSnapshotObservedEvent>(),Arg.Any<CancellationToken>()).Returns(_=>{published.TrySetResult();return ValueTask.CompletedTask;});
        await using var bridge=new BrokerAccountObservationBridge(broker,supervisor,NullLogger<BrokerAccountObservationBridge>.Instance,TimeSpan.FromMilliseconds(10));await bridge.StartAsync();
        await Task.WhenAll(restarted.Task,published.Task).WaitAsync(TimeSpan.FromSeconds(5));Assert.True(resyncs>=2);Assert.True(readers>=2);await bridge.StopAsync();
    }
    [Fact]
    public async Task Owner_checkpoint_freeze_and_publication_precede_normal_same_revision_polling()
    {
        var broker=Substitute.For<ITradeBroker>();broker.AccountAlias.Returns("EMU");broker.Environment.Returns(BrokerEnvironment.Emulator);
        var polled=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var later=Snapshot() with{AsOfUtc=Snapshot().AsOfUtc.AddSeconds(1),SourceSequence=2};
        broker.ResynchronizeAccountAsync(Arg.Any<CancellationToken>()).Returns(later);broker.GetAccountSnapshotAsync(Arg.Any<CancellationToken>()).Returns(_=>{polled.TrySetResult();return ValueTask.FromResult(later);});
        broker.ObserveAccountAsync(Arg.Any<CancellationToken>()).Returns(c=>Quiet(c.Arg<CancellationToken>()));
        var supervisor=Substitute.For<IActorSupervisor>();supervisor.IsReady.Returns(true);var producer=Substitute.For<IJSActorProducer>();supervisor.GetJSProducer(Arg.Any<ActorMailboxId>()).Returns(producer);
        var observed=new List<BrokerAccountSnapshotEvidence>();var both=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        producer.SendAsync<BrokerAccountSnapshotObservedEvent,BrokerAccountId>(Arg.Any<ActorSubject>(),Arg.Any<BrokerAccountSnapshotObservedEvent>(),Arg.Any<CancellationToken>()).Returns(c=>{lock(observed){observed.Add(c.Arg<BrokerAccountSnapshotObservedEvent>().Snapshot);if(observed.Count>=1)both.TrySetResult();}return ValueTask.CompletedTask;});
        await using var bridge=new BrokerAccountObservationBridge(broker,supervisor,NullLogger<BrokerAccountObservationBridge>.Instance,TimeSpan.FromMilliseconds(10));
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frozen=BrokerAccountSnapshotEvidence.From(Snapshot() with{SourceSequence=2});
        var owner=bridge.PublishOwnerCheckpointAsync(async ct=>{entered.TrySetResult();await release.Task.WaitAsync(ct);return frozen;}).AsTask();
        await entered.Task;await bridge.StartAsync();await Task.Yield();await broker.DidNotReceive().ResynchronizeAccountAsync(Arg.Any<CancellationToken>());
        release.TrySetResult();await owner;await both.Task.WaitAsync(TimeSpan.FromSeconds(5));await polled.Task.WaitAsync(TimeSpan.FromSeconds(5));Assert.Single(observed);Assert.Equal(frozen,observed[0]);
    }}
