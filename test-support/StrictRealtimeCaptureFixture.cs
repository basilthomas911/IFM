using System.Reflection;
using MessagePack;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.TestSupport;

/// <summary>Uses the production strict codec and durable source outbox, without a broker or substituted capture receipts.</summary>
public sealed class StrictRealtimeCaptureFixture : IAsyncDisposable
{
    readonly object store;
    readonly MethodInfo capture, encode;
    readonly IAsyncDisposable lifetime;
    public StrictRealtimeCaptureFixture(string directory)
    {
        var assembly = typeof(DurableRealtimeMailboxHub).Assembly;
        var codec = assembly.GetType("TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes.RealtimeMailboxCodec", true)!;
        var outbox = assembly.GetType("TomasAI.IFM.Framework.Messaging.NatsJetStream.RealtimeMailboxes.RealtimeSourceOutbox", true)!;
        encode = codec.GetMethod("Capture", BindingFlags.Static | BindingFlags.Public)!;
        capture = outbox.GetMethod("CaptureAsync", BindingFlags.Instance | BindingFlags.Public)!;
        store = Activator.CreateInstance(outbox, [new RealtimeMailboxOptions { Directory = directory, Epoch = "strict_retry" }, false])!;
        lifetime = (IAsyncDisposable)store;
    }

    public ValueTask<RealtimeCaptureReceipt> CaptureAsync<T>(T value) where T : class, IEvent
    {
        var publication = new RealtimeSourcePublication(value.Subject, value.Id,
            MessagePackSerializer.Serialize(value), MessageOrigins.ForPublication(value, TimeProvider.System));
        var encoded = encode.Invoke(null, [publication, new[] { value.Subject }, 512 * 1024])!;
        try { return (ValueTask<RealtimeCaptureReceipt>)capture.Invoke(store, [encoded, CancellationToken.None])!; }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    public ValueTask DisposeAsync() => lifetime.DisposeAsync();
}
