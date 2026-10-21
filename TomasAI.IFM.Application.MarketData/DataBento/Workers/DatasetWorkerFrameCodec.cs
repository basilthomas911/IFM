using System.Buffers;
using System.Buffers.Binary;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public static class DatasetWorkerFrameCodec
{
    public static async ValueTask WriteAsync(
        Stream stream,
        DatasetWorkerControlFrame frame,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Validate(frame);
        var payload = MessagePackSerializer.Serialize(frame, MessagePackBinarySerializer.ContentOptions,cancellationToken:cancellationToken);
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
        => await ReadAsync(stream, maximumBytes, cancellationToken, ArrayPool<byte>.Shared).ConfigureAwait(false);

    // The buffer is borrowed only through synchronous deserialization. Frame contracts own their strings,
    // arrays and immutable collections; none expose a slice of the MessagePack input memory.
    internal static async ValueTask<DatasetWorkerControlFrame> ReadAsync(
        Stream stream, int maximumBytes, CancellationToken cancellationToken, ArrayPool<byte> pool)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prefix = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length is < 2 || length > maximumBytes)
            throw new InvalidDataException($"Worker control frame length {length} is invalid.");
        var payload = pool.Rent(length);
        try
        {
            await stream.ReadExactlyAsync(payload.AsMemory(0, length), cancellationToken).ConfigureAwait(false);
            var frame = MessagePackSerializer.Deserialize<DatasetWorkerControlFrame>(payload.AsMemory(0, length),
                MessagePackBinarySerializer.ContentOptions.WithSecurity(MessagePackSecurity.UntrustedData), cancellationToken: cancellationToken)
                ?? throw new InvalidDataException("Worker control frame is empty.");
            Validate(frame);
            return frame;
        }
        finally
        {
            // Frames contain a bootstrap token. Clear rented storage before sharing it with another reader.
            pool.Return(payload, clearArray: true);
        }
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
            if (!string.Equals(manifest.Dataset,frame.Dataset,StringComparison.Ordinal) || manifest.ValueDate != frame.ValueDate
                || manifest.Revision != frame.ManifestRevision
                || !string.Equals(manifest.Fingerprint,frame.ManifestFingerprint,StringComparison.Ordinal))
                throw new InvalidDataException("Worker manifest identity does not match its control frame.");
        }
        if (frame.OptionChain is { } chain && (chain.GenerationId != frame.GenerationId || chain.ValueDate != frame.ValueDate
            || chain.Options.IsDefaultOrEmpty || chain.Options.Length > 2048
            || chain.Options.Any(x => !string.Equals(x?.Pricing?.Contract?.Dataset,frame.Dataset,StringComparison.Ordinal))))
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
            if (!string.Equals(diagnostics.Dataset,frame.Dataset,StringComparison.Ordinal) || diagnostics.GenerationId != frame.GenerationId)
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
