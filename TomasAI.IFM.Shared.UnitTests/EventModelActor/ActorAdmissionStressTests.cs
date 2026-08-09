using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Shared.UnitTests.EventModelActor;

public sealed class ActorAdmissionStressTests(ITestOutputHelper output)
{
    [Fact]
    public void MixedTraffic_ConcurrentReserveRelease_RemainsBoundedAndDrainsToZero()
    {
        const int workerCount = 8;
        const int operationsPerWorker = 50_000;
        var options = CreateOptions(globalMessages: 1_024, mailboxMessages: 128);
        var controller = new ActorAdmissionController(options);
        var actorTypes = new[]
        {
            ActorType.Command,
            ActorType.Query,
            ActorType.Event,
            ActorType.Notify,
            ActorType.UI
        };
        var messages = actorTypes.Select((actorType, index) =>
            new StressMessage(actorType, 128 + index * 32)).ToArray();
        var accepted = 0L;
        var rejected = 0L;
        var maximumInUse = 0L;
        var process = Process.GetCurrentProcess();
        process.Refresh();
        var cpuBefore = process.TotalProcessorTime;
        var workingSetBefore = process.WorkingSet64;
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var stopwatch = Stopwatch.StartNew();

        Parallel.For(
            0,
            workerCount,
            new ParallelOptions { MaxDegreeOfParallelism = workerCount },
            worker =>
            {
                for (var index = 0; index < operationsPerWorker; index++)
                {
                    var actorType = actorTypes[(worker + index) % actorTypes.Length];
                    var message = messages[(worker + index) % messages.Length];
                    var result = controller.TryReserve(message, actorType, out var charge);
                    if (!result.Accepted)
                    {
                        Interlocked.Increment(ref rejected);
                        continue;
                    }

                    Interlocked.Increment(ref accepted);
                    UpdateMaximum(ref maximumInUse, controller.CurrentMessageCount);
                    controller.Release(charge);
                }
            });

        stopwatch.Stop();
        process.Refresh();
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var cpu = process.TotalProcessorTime - cpuBefore;
        var workingSetChange = process.WorkingSet64 - workingSetBefore;
        var operations = workerCount * operationsPerWorker;
        output.WriteLine(
            "Mixed traffic: {0:N0} operations, {1:N0} ops/s, elapsed {2:F3}s, CPU {3:F3}s, process allocations {4:N0} bytes, working-set change {5:N0} bytes, peak reservations {6}.",
            operations,
            operations / stopwatch.Elapsed.TotalSeconds,
            stopwatch.Elapsed.TotalSeconds,
            cpu.TotalSeconds,
            allocated,
            workingSetChange,
            maximumInUse);

        accepted.Should().Be(operations);
        rejected.Should().Be(0);
        maximumInUse.Should().BeLessThanOrEqualTo(options.GlobalMessageLimit);
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
    }

    [Fact]
    public void HotMailbox_SustainedOverloadRejectsImmediatelyAndReleasesAllCapacity()
    {
        const int attempts = 100_000;
        const int mailboxCapacity = 64;
        var controller = new ActorAdmissionController(
            CreateOptions(globalMessages: 256, mailboxMessages: mailboxCapacity));
        using var queue = new ActorThreadQueueV2(controller, mailboxCapacity);
        queue.SetId(new ActorThreadId(ActorType.Command, "Stress", "hot"));
        queue.Start();
        var scheduled = (IScheduledActorThreadQueue)queue;
        var message = new StressMessage(ActorType.Command, 256);
        var accepted = 0;
        var stopwatch = Stopwatch.StartNew();

        for (var index = 0; index < attempts; index++)
        {
            if (queue.Write(message))
                accepted++;
        }

        stopwatch.Stop();
        output.WriteLine(
            "Hot mailbox: {0:N0} attempts, {1:N0} attempts/s, accepted {2}, rejected {3}.",
            attempts,
            attempts / stopwatch.Elapsed.TotalSeconds,
            accepted,
            attempts - accepted);

        accepted.Should().Be(mailboxCapacity);
        queue.Count.Should().Be(mailboxCapacity);
        controller.CurrentMessageCount.Should().Be(mailboxCapacity);
        while (scheduled.TryRead(out _))
        {
        }
        queue.Count.Should().Be(0);
        controller.CurrentMessageCount.Should().Be(0);
        controller.CurrentByteCount.Should().Be(0);
    }

    static ActorAdmissionOptions CreateOptions(long globalMessages, int mailboxMessages)
        => new()
        {
            Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = globalMessages,
            GlobalByteLimit = globalMessages * 1_024,
            MaximumPayloadBytes = 1_024,
            DefaultActorTypeMessageLimit = globalMessages,
            DefaultActorTypeByteLimit = globalMessages * 1_024,
            DefaultMailboxMessageLimit = mailboxMessages,
            JetStreamNakDelayMilliseconds = 150,
            OverloadErrorCode = -429
        };

    static void UpdateMaximum(ref long target, long value)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (value <= current || Interlocked.CompareExchange(ref target, value, current) == current)
                return;
        }
    }

    sealed class StressMessage(ActorType actorType, int admissionSizeBytes) : IActorMessage
    {
        public int AdmissionSizeBytes { get; } = admissionSizeBytes;
        public ActorSubject Subject { get; } = new(actorType, "Stress", "Run", "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>()
            where TQuery : class, IQuery<TResult>
            where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
