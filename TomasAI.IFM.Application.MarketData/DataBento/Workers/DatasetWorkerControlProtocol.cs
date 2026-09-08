using System.Buffers.Binary;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;

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
    ManifestRejected = 16,
    AcquireOptionChain = 17,
    ReleaseOptionChain = 18,
    OptionChainResult = 19,
    CaptureCompositionSnapshot = 20,
    CompositionSnapshotResult = 21,
    ApplyOptionChainOwnership = 22
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
    [Key(17)] public WorkerOptionChainRequest? OptionChain { get; init; }
    [Key(18)] public WorkerOptionChainRelease? OptionChainRelease { get; init; }
    [Key(19)] public WorkerOptionChainResult? OptionChainResult { get; init; }
    [Key(20)] public CompositionSnapshotRequest? CompositionRequest { get; init; }
    [Key(21)] public CompositionSnapshotResult? CompositionResult { get; init; }
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
        var payload = MessagePackSerializer.Serialize(frame, MessagePackBinarySerializer.ContentOptions);
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
            MessagePackBinarySerializer.ContentOptions.WithSecurity(MessagePackSecurity.UntrustedData))
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
        if (frame.OptionChain is { } chain && (chain.GenerationId != frame.GenerationId || chain.ValueDate != frame.ValueDate
            || chain.Options.IsDefaultOrEmpty || chain.Options.Length > 512
            || chain.Options.Any(x => x?.Pricing?.Contract?.Dataset != frame.Dataset)))
            throw new InvalidDataException("Option chain scope does not match the worker identity.");
        if (frame.CompositionRequest is { } capture && capture.GenerationId != frame.GenerationId
            || frame.OptionChainRelease is { } release && release.GenerationId != frame.GenerationId)
            throw new InvalidDataException("Option operation belongs to another generation.");
        if (frame.Kind == DatasetWorkerMessageKind.AcquireOptionChain && frame.OptionChain is null
            || frame.Kind == DatasetWorkerMessageKind.ReleaseOptionChain && (frame.OptionChainRelease is null || frame.OptionChainRelease.Ownership is not null)
            || frame.Kind == DatasetWorkerMessageKind.ApplyOptionChainOwnership && (frame.OptionChainRelease?.Ownership is null || frame.OptionChainRelease.LeaseId != Guid.Empty)
            || frame.Kind == DatasetWorkerMessageKind.OptionChainResult && frame.OptionChainResult is null
            || frame.Kind == DatasetWorkerMessageKind.CaptureCompositionSnapshot && frame.CompositionRequest is null
            || frame.Kind == DatasetWorkerMessageKind.CompositionSnapshotResult && frame.CompositionResult is null)
            throw new InvalidDataException("Option operation requires its typed payload.");
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
