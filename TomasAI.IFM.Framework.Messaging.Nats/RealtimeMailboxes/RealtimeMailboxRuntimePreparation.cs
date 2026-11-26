using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;

namespace TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;

/// <summary>Prepares known code and local control-plane metadata, never business inputs or broker resources.</summary>
internal static class RealtimeMailboxRuntimePreparation
{
    internal sealed record Result(int Assemblies, int PreparedMethods, int ControlModels, int ControlBytes);
    const string SchedulerNamespace = "TomasAI.IFM.Application.EventModelActor";
    const int MaximumMethods = 4096;
    static readonly Lock Gate = new();
    static readonly Dictionary<Assembly, Task<Result>> Preparations = new();
    static int reservedMethods;

    internal static async Task<Result> PrepareAsync(Type? schedulerType, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var own = typeof(RealtimeMailboxRuntimePreparation).Assembly;
        Assembly? scheduler = null;
        if (schedulerType is not null)
        {
            if (schedulerType.Namespace is not { } name || !(name == SchedulerNamespace || name.StartsWith(SchedulerNamespace + ".", StringComparison.Ordinal)))
                throw new ArgumentException("Only the actual actor scheduler namespace may be prepared.", nameof(schedulerType));
            scheduler = schedulerType.Assembly;
        }
        Task<Result>[] tasks;
        lock (Gate)
        {
            var selected = scheduler is null || scheduler == own ? new[] { own } : new[] { own, scheduler };
            if (Preparations.Keys.Concat(selected).Distinct().Count() > 2)
                throw new InvalidOperationException("Realtime preparation assembly bound exceeded.");
            tasks = selected.Select(assembly =>
            {
                if (Preparations.TryGetValue(assembly, out var existing)) return existing;
                var local = assembly == own;
                var methods = assembly.GetTypes()
                    .Where(type => type.Namespace is { } ns && (local
                        ? ns == typeof(RealtimeMailboxRuntimePreparation).Namespace
                        : ns == SchedulerNamespace || ns.StartsWith(SchedulerNamespace + ".", StringComparison.Ordinal)))
                    .Where(type => !type.ContainsGenericParameters)
                    .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Cast<MethodBase>()
                        .Concat(type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)))
                    .Where(method => !method.ContainsGenericParameters && !method.IsAbstract && method.GetMethodBody() is not null)
                    .ToArray();
                if (reservedMethods + methods.Length > MaximumMethods)
                    throw new InvalidOperationException("Realtime preparation method bound exceeded.");
                reservedMethods += methods.Length;
                var task = Task.Run(() =>
                {
                    var models = local ? PrepareControlModels() : (Count: 0, Bytes: 0);
                    foreach (var method in methods) RuntimeHelpers.PrepareMethod(method.MethodHandle);
                    return new Result(1, methods.Length, models.Count, models.Bytes);
                });
                Preparations.Add(assembly, task);
                return task;
            }).ToArray();
        }
        var results = await Task.WhenAll(tasks).WaitAsync(token).ConfigureAwait(false);
        return new(results.Sum(result => result.Assemblies), results.Sum(result => result.PreparedMethods), results.Sum(result => result.ControlModels), results.Sum(result => result.ControlBytes));
    }

    static (int Count, int Bytes) PrepareControlModels()
    {
        var count = 0; var bytes = 0;
        var stream = new StreamConfig("ifm_local_metadata", ["ifm.local.metadata.>"])
        { Storage = StreamConfigStorage.File, Retention = StreamConfigRetention.Workqueue, Discard = StreamConfigDiscard.New, MaxBytes = 1024, MaxMsgSize = 1024, NumReplicas = 1 };
        var consumer = new ConsumerConfig("ifm_local_metadata")
        { AckPolicy = ConsumerConfigAckPolicy.Explicit, MaxAckPending = 1, FilterSubject = "ifm.local.metadata.one" };
        RoundTrip(stream);RoundTrip(consumer);
        RoundTrip(new ConsumerGetnextRequest { Batch = 1, NoWait = true });
        RoundTrip(new ConsumerInfo { StreamName = stream.Name!, Name = consumer.Name!, Config = consumer });
        RoundTrip(new ConsumerCreateResponse { StreamName = stream.Name!, Name = consumer.Name!, Config = consumer });
        RoundTrip(new ConsumerInfoResponse { StreamName = stream.Name!, Name = consumer.Name!, Config = consumer });
        RoundTrip(new StreamInfo { Config = stream });
        RoundTrip(new StreamCreateResponse { Config = stream });
        RoundTrip(new StreamInfoResponse { Config = stream });
        RoundTrip(new StreamInfoRequest());
        RoundTrip(new PubAckResponse { Stream = stream.Name, Seq = 1 });
        return (count, bytes);

        void RoundTrip<T>(T value)
        {
            var buffer = new BoundedBuffer();
            var serializer = NatsJSJsonSerializer<T>.Default;
            serializer.Serialize(buffer, value);
            var sequence = new ReadOnlySequence<byte>(buffer.WrittenMemory);
            if (serializer.Deserialize(sequence) is null) throw new InvalidDataException("Control metadata preparation returned null.");
            count++;bytes += buffer.WrittenCount;
        }
    }
    sealed class BoundedBuffer : IBufferWriter<byte>
    {
        const int MaximumBytes = 65536;
        readonly byte[] bytes = new byte[MaximumBytes];
        internal int WrittenCount { get; private set; }
        internal ReadOnlyMemory<byte> WrittenMemory => bytes.AsMemory(0, WrittenCount);
        public void Advance(int count)
        {
            if (count < 0 || count > MaximumBytes - WrittenCount) throw new InvalidDataException("Control metadata preparation exceeds 64 KiB.");
            WrittenCount += count;
        }
        public Memory<byte> GetMemory(int sizeHint = 0) { Check(sizeHint);return bytes.AsMemory(WrittenCount); }
        public Span<byte> GetSpan(int sizeHint = 0) { Check(sizeHint);return bytes.AsSpan(WrittenCount); }
        void Check(int sizeHint)
        {
            if (sizeHint < 0 || sizeHint > MaximumBytes - WrittenCount) throw new InvalidDataException("Control metadata preparation exceeds 64 KiB.");
        }
    }
}
