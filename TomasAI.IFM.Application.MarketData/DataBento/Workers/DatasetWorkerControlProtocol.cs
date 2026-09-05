using System.Buffers.Binary;
using MessagePack;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public enum DatasetWorkerMessageKind : byte
{
    WorkerHello = 1,
    SupervisorHello = 2,
    HealthSnapshot = 3,
    CooperativeReset = 4,
    ResetCompleted = 5,
    GracefulStop = 6,
    Stopped = 7,
    TerminalFault = 8,
    ProtocolError = 9,
    Hang = 10,
    WorkerReady = 11,
    StartManifest = 12,
    StartAccepted = 13,
    ApplySubscriptionManifest = 14,
    SubscriptionManifestApplied = 15,
    ManifestRejected = 16
}

[MessagePackObject]
public sealed record DatasetWorkerControlFrame
{
    public const int CurrentProtocolMajor = 2;
    [Key(0)] public int ProtocolMajor { get; init; } = CurrentProtocolMajor;
    [Key(1)] public int ProtocolMinor { get; init; }
    [Key(2)] public required DatasetWorkerMessageKind Kind { get; init; }
    [Key(3)] public required Guid WorkerInstanceId { get; init; }
    [Key(4)] public required string Dataset { get; init; }
    [Key(5)] public required DateOnly ValueDate { get; init; }
    [Key(6)] public required Guid GenerationId { get; init; }
    [Key(7)] public required Guid CorrelationId { get; init; }
    [Key(8)] public required long Sequence { get; init; }
    [Key(9)] public int ProcessId { get; init; }
    [Key(10)] public bool Healthy { get; init; }
    [Key(11)] public string Detail { get; init; } = string.Empty;
    [Key(12)] public required string BootstrapToken { get; init; }
    [Key(13)] public DatasetSubscriptionManifest? Manifest { get; init; }
    [Key(14)] public long ManifestRevision { get; init; }
    [Key(15)] public string ManifestFingerprint { get; init; } = string.Empty;
    [Key(16)] public DatasetWorkerDiagnostics? Diagnostics { get; init; }
}

public static class DatasetWorkerFrameCodec
{
    public static async ValueTask WriteAsync(
        Stream stream,
        DatasetWorkerControlFrame frame,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        Validate(frame);
        var payload = MessagePackSerializer.Serialize(frame);
        if (payload.Length > maximumBytes)
            throw new InvalidDataException($"Worker control frame exceeds {maximumBytes} bytes.");
        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public static async ValueTask<DatasetWorkerControlFrame> ReadAsync(
        Stream stream,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is < 2 || length > maximumBytes)
            throw new InvalidDataException($"Worker control frame length {length} is invalid.");
        var payload = GC.AllocateUninitializedArray<byte>(length);
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        var frame = MessagePackSerializer.Deserialize<DatasetWorkerControlFrame>(payload,
            MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData))
            ?? throw new InvalidDataException("Worker control frame is empty.");
        Validate(frame);
        return frame;
    }

    static void Validate(DatasetWorkerControlFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (frame.ProtocolMajor != DatasetWorkerControlFrame.CurrentProtocolMajor
            || !Enum.IsDefined(frame.Kind)
            || frame.WorkerInstanceId == Guid.Empty
            || string.IsNullOrWhiteSpace(frame.Dataset)
            || frame.Dataset.Length > 64
            || frame.ValueDate == default
            || frame.GenerationId == Guid.Empty
            || frame.CorrelationId == Guid.Empty
            || frame.Sequence < 1
            || frame.Detail is null || frame.Detail.Length > 4096
            || frame.BootstrapToken is null || frame.BootstrapToken.Length != 64
            || frame.ManifestRevision < 0
            || frame.ManifestFingerprint is null || frame.ManifestFingerprint.Length > 64)
            throw new InvalidDataException("Worker control frame identity or bounds are invalid.");
        if (frame.Manifest is { } manifest)
        {
            manifest.Validate();
            if (manifest.Dataset != frame.Dataset || manifest.ValueDate != frame.ValueDate
                || manifest.Revision != frame.ManifestRevision
                || manifest.Fingerprint != frame.ManifestFingerprint)
                throw new InvalidDataException("Worker manifest identity does not match its control frame.");
        }
        if (frame.Diagnostics is { } diagnostics)
        {
            diagnostics.Validate();
            if (diagnostics.Dataset != frame.Dataset || diagnostics.GenerationId != frame.GenerationId)
                throw new InvalidDataException("Worker diagnostics do not match the control-frame dataset/generation.");
        }
        if (frame.Kind is DatasetWorkerMessageKind.StartManifest
            or DatasetWorkerMessageKind.ApplySubscriptionManifest
            or DatasetWorkerMessageKind.CooperativeReset && frame.Manifest is null)
            throw new InvalidDataException("This worker command requires a complete subscription manifest.");
        if (frame.Kind is DatasetWorkerMessageKind.StartAccepted
            or DatasetWorkerMessageKind.SubscriptionManifestApplied
            or DatasetWorkerMessageKind.ResetCompleted
            && (frame.ManifestRevision < 1 || frame.ManifestFingerprint.Length != 64))
            throw new InvalidDataException("Worker acknowledgment requires the realized revision and fingerprint.");
    }
}
