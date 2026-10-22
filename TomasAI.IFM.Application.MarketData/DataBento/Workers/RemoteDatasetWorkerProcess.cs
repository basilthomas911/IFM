using TomasAI.IFM.Framework.Messaging.NatsJetStream.Serializers;
using MessagePack;
using NATS.Client.Core;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

/// <summary>Controls a child owned by MarketData Host over NATS; stop never queues behind ordinary commands.</summary>
public sealed class RemoteDatasetWorkerProcess(MarketDataHostConnection connection, string role,
    Func<DatasetPublicationEnvelope, CancellationToken, ValueTask> ingress) : IDatasetWorkerProcess
{
    readonly CancellationTokenSource stopping = new();
    DatasetWorkerProcessSnapshot current = new() { Dataset = "", WorkerInstanceId = Guid.Empty,
        GenerationId = Guid.Empty, ProcessId = 0, StartedOnUtc = default, Running = false,
        Healthy = false, GracefulStopSucceeded = false, ForcedTermination = false };
    Task? reader;
    Guid workerId;
    INatsSub<DatasetPublicationEnvelope>? subscription;
    int disposed;
    public event Action<DatasetWorkerProcessSnapshot>? Exited;
    public DatasetWorkerProcessSnapshot Current => Volatile.Read(ref current);
    string Route(string action) => $"workers/{role}/{workerId:D}/{action}";
    public async Task<DatasetWorkerProcessSnapshot> StartAsync(DatasetWorkerStartRequest request, CancellationToken cancellationToken = default)
    {
        workerId = request.WorkerInstanceId;
        var client = await connection.GetClientAsync(cancellationToken).ConfigureAwait(false);
        subscription = await client.Connection.SubscribeCoreAsync<DatasetPublicationEnvelope>(connection.Publications(role, workerId),
            serializer: NatsMessagePackSerializer<DatasetPublicationEnvelope>.Default, cancellationToken: stopping.Token).ConfigureAwait(false);
        await client.PingAsync(cancellationToken).ConfigureAwait(false);
        reader = ReadAsync(stopping.Token);
        try
        {
            var result = await connection.PostAsync<DatasetWorkerProcessSnapshot>($"workers/{role}/start",
                new MarketDataWorkerStart(request.Manifest!, workerId, request.GenerationId), cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref current, result);
            return result;
        }
        catch { await TerminateAsync().ConfigureAwait(false); throw; }
    }
    async Task ReadAsync(CancellationToken token)
    {
        try
        {
            await foreach (var message in subscription!.Msgs.ReadAllAsync(token).ConfigureAwait(false))
            {
                var frame = message.Data ?? throw new InvalidDataException("Missing market publication.");
                if (frame.WorkerInstanceId != workerId) throw new InvalidDataException("Worker publication identity changed.");
                await ingress(frame, token).ConfigureAwait(false);
                // Only durable evidence requests a receipt, after application persistence succeeds.
                if (frame.Kind == DatasetPublicationKind.OptionTradeEvidence && !string.IsNullOrEmpty(message.ReplyTo))
                    await message.ReplyAsync(true, cancellationToken: token).ConfigureAwait(false);
            }
        }
        catch (Exception) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            Volatile.Write(ref current, Current with { Healthy = false, DataPlaneHealthy = false, Detail = error.Message });
            Exited?.Invoke(Current);
        }
    }
    async Task<DatasetWorkerControlFrame> ControlAsync(string action, object request, CancellationToken token)
    {
        try
        {
            var frame = await connection.PostAsync<DatasetWorkerControlFrame>(Route(action), request, token).ConfigureAwait(false);
            if (frame.WorkerInstanceId != workerId) throw new InvalidDataException("Control response identity changed.");
            Volatile.Write(ref current, Current with { GenerationId = frame.GenerationId, Healthy = frame.Healthy,
                ProcessId = frame.ProcessId, Running = frame.Kind != DatasetWorkerMessageKind.Stopped,
                ControlResponsive = true, ManifestRevision = frame.ManifestRevision, ManifestFingerprint = frame.ManifestFingerprint,
                Diagnostics = frame.Diagnostics, Detail = frame.Detail });
            return frame;
        }
        catch { Volatile.Write(ref current, Current with { Healthy = false, ControlResponsive = false }); throw; }
    }
    public Task<DatasetWorkerControlFrame> GetHealthAsync(CancellationToken cancellationToken = default) => ControlAsync("health", new { }, cancellationToken);
    public Task<DatasetWorkerControlFrame> ResetAsync(DatasetSubscriptionManifest manifest, CancellationToken cancellationToken = default) => ControlAsync("reset", manifest, cancellationToken);
    public Task<DatasetWorkerControlFrame> ApplyManifestAsync(DatasetSubscriptionManifest manifest, CancellationToken cancellationToken = default) => ControlAsync("manifest", manifest, cancellationToken);
    public async Task<DatasetWorkerProcessSnapshot> StopAsync(CancellationToken cancellationToken = default)
    { await TerminateAsync().ConfigureAwait(false); return Current; }
    public async Task TerminateAsync()
    {
        if (workerId == Guid.Empty) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _ = await connection.PostAsync<bool>(Route("stop"), new { }, timeout.Token).ConfigureAwait(false);
        Volatile.Write(ref current, Current with { Running = false, Healthy = false, ForcedTermination = true, ExitCode = 0 });
        await stopping.CancelAsync().ConfigureAwait(false);
    }
    public Task<byte[]> InvokeMarketDataApiAsync(Remote.MarketDataApiCall request, CancellationToken cancellationToken)
        => connection.PostAsync<byte[]>(Route("api"), request, cancellationToken);
    public Task<WorkerOptionChainResult> AcquireOptionChainAsync(WorkerOptionChainRequest request, CancellationToken cancellationToken)
        => connection.PostAsync<WorkerOptionChainResult>(Route("acquire"), request, cancellationToken);
    public Task<WorkerOptionChainResult> ReleaseOptionChainAsync(WorkerOptionChainRelease request, CancellationToken cancellationToken)
        => connection.PostAsync<WorkerOptionChainResult>(Route("release"), request, cancellationToken);
    public Task<WorkerOptionChainResult> ApplyOptionChainOwnershipAsync(WorkerOptionChainRelease request, CancellationToken cancellationToken)
        => connection.PostAsync<WorkerOptionChainResult>(Route("ownership"), request, cancellationToken);
    public Task<CompositionSnapshotResult> CaptureCompositionSnapshotAsync(CompositionSnapshotRequest request, CancellationToken cancellationToken)
        => connection.PostAsync<CompositionSnapshotResult>(Route("capture"), request, cancellationToken);
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        await TerminateAsync().ConfigureAwait(false);
        if (subscription is not null) await subscription.DisposeAsync().ConfigureAwait(false);
        if (reader is not null) await reader.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        stopping.Dispose();
    }
}
