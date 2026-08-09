using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Framework.Messaging.Nats.Benchmarks;

/// <summary>Measures the dormant and observe-only SWO-02 admission-accounting paths.</summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorAdmissionBenchmarks
{
    ActorThreadQueueV2 _queue = null!;
    IScheduledActorThreadQueue _scheduled = null!;
    BenchmarkActorMessage _message = null!;

    [Params(ActorAdmissionMode.Disabled, ActorAdmissionMode.ObserveOnly, ActorAdmissionMode.Enforce)]
    public ActorAdmissionMode Mode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var options = new ActorAdmissionOptions
        {
            Mode = Mode,
            GlobalMessageLimit = 65_536,
            GlobalByteLimit = 256L * 1024 * 1024,
            MaximumPayloadBytes = 1024 * 1024,
            DefaultActorTypeMessageLimit = 32_768,
            DefaultActorTypeByteLimit = 128L * 1024 * 1024,
            DefaultMailboxMessageLimit = 1024
        };
        var controller = new ActorAdmissionController(options);
        _queue = new ActorThreadQueueV2(controller, 1024);
        _queue.SetId(new ActorThreadId(ActorType.Command, "AdmissionBenchmark", "1"));
        _queue.Start();
        _scheduled = _queue;
        _message = new BenchmarkActorMessage();
    }

    [GlobalCleanup]
    public void Cleanup() => _queue.Dispose();

    [Benchmark(OperationsPerInvoke = 256)]
    public int ScheduledEnqueueDequeueBatch()
    {
        var read = 0;
        for (var index = 0; index < 256; index++)
        {
            _ = _scheduled.TryWrite(_message, default);
            _ = _scheduled.TrySchedule();
            if (_scheduled.TryRead(out _))
                read++;
            _ = _scheduled.CompleteDrain();
        }

        return read;
    }

    sealed class BenchmarkActorMessage : IActorMessage
    {
        public int AdmissionSizeBytes => 256;
        public ActorSubject Subject { get; } = new(ActorType.Command, "AdmissionBenchmark", "Run", "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}

/// <summary>Measures each allocation-free enforced rejection decision.</summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(8)]
public class ActorAdmissionRejectionBenchmarks
{
    ActorAdmissionController _globalCount = null!;
    ActorAdmissionController _globalByte = null!;
    ActorAdmissionController _actorTypeCount = null!;
    ActorAdmissionController _actorTypeByte = null!;
    ActorAdmissionController _payload = null!;
    ActorAdmissionCharge _globalCountCharge;
    ActorAdmissionCharge _globalByteCharge;
    ActorAdmissionCharge _actorTypeCharge;
    ActorAdmissionCharge _actorTypeByteCharge;
    ActorThreadQueueV2 _mailbox = null!;
    AdmissionBenchmarkMessage _small = null!;
    AdmissionBenchmarkMessage _large = null!;

    [GlobalSetup]
    public void Setup()
    {
        _small = new AdmissionBenchmarkMessage(1);
        _large = new AdmissionBenchmarkMessage(256);

        _globalCount = new ActorAdmissionController(CreateOptions(1, 256, 1, 256, 256));
        _ = _globalCount.TryReserve(_large, ActorType.Command, out _globalCountCharge);

        _globalByte = new ActorAdmissionController(CreateOptions(2, 256, 2, 256, 256));
        _ = _globalByte.TryReserve(_large, ActorType.Command, out _globalByteCharge);

        _actorTypeCount = new ActorAdmissionController(CreateOptions(2, 512, 1, 512, 256));
        _ = _actorTypeCount.TryReserve(_large, ActorType.Command, out _actorTypeCharge);

        _actorTypeByte = new ActorAdmissionController(CreateOptions(2, 512, 2, 256, 256));
        _ = _actorTypeByte.TryReserve(_large, ActorType.Command, out _actorTypeByteCharge);

        _payload = new ActorAdmissionController(CreateOptions(2, 512, 2, 512, 128));

        var mailboxController = new ActorAdmissionController(CreateOptions(2, 512, 2, 512, 256));
        _mailbox = new ActorThreadQueueV2(mailboxController, capacity: 1);
        _mailbox.SetId(new ActorThreadId(ActorType.Command, "AdmissionRejection", "1"));
        _mailbox.Start();
        _ = _mailbox.Write(_small);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _globalCount.Release(_globalCountCharge);
        _globalByte.Release(_globalByteCharge);
        _actorTypeCount.Release(_actorTypeCharge);
        _actorTypeByte.Release(_actorTypeByteCharge);
        _mailbox.Dispose();
    }

    [Benchmark(Baseline = true)]
    public ActorAdmissionReason GlobalMessageLimit()
        => _globalCount.TryReserve(_small, ActorType.Command, out _).Reason;

    [Benchmark]
    public ActorAdmissionReason GlobalByteLimit()
        => _globalByte.TryReserve(_small, ActorType.Command, out _).Reason;

    [Benchmark]
    public ActorAdmissionReason ActorTypeMessageLimit()
        => _actorTypeCount.TryReserve(_small, ActorType.Command, out _).Reason;

    [Benchmark]
    public ActorAdmissionReason ActorTypeByteLimit()
        => _actorTypeByte.TryReserve(_small, ActorType.Command, out _).Reason;

    [Benchmark]
    public ActorAdmissionReason PayloadTooLarge()
        => _payload.TryReserve(_large, ActorType.Command, out _).Reason;

    [Benchmark]
    public bool MailboxLimit() => _mailbox.Write(_small);

    static ActorAdmissionOptions CreateOptions(
        long globalMessages,
        long globalBytes,
        long typeMessages,
        long typeBytes,
        int maximumPayloadBytes)
        => new()
        {
            Mode = ActorAdmissionMode.Enforce,
            GlobalMessageLimit = globalMessages,
            GlobalByteLimit = globalBytes,
            MaximumPayloadBytes = maximumPayloadBytes,
            DefaultActorTypeMessageLimit = typeMessages,
            DefaultActorTypeByteLimit = typeBytes,
            DefaultMailboxMessageLimit = 1
        };

    sealed class AdmissionBenchmarkMessage(int admissionSizeBytes) : IActorMessage
    {
        public int AdmissionSizeBytes { get; } = admissionSizeBytes;
        public ActorSubject Subject { get; } = new(ActorType.Command, "AdmissionRejection", "Run", "1");
        public ActorSubject ReplySubject { get; set; }
        public TCommand? AsCommand<TCommand>() where TCommand : class, ICommand => default;
        public TEvent? AsEvent<TEvent>() where TEvent : class, IEvent => default;
        public TQuery? AsQuery<TQuery, TResult>() where TQuery : class, IQuery<TResult> where TResult : class => default;
        public ValueTask ReplyAsync<TResult>(TResult result) where TResult : class => ValueTask.CompletedTask;
        public void ReleasePayload() { }
        public NatsMsg<byte[]> GetMessage() => default;
        public void Dispose() { }
    }
}
