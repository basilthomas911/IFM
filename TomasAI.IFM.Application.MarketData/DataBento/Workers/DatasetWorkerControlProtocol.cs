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
    WorkerReady = 11
}

[MessagePackObject]
public sealed record DatasetWorkerControlFrame
{
    public const int CurrentProtocolMajor = 1;
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
        var frame = MessagePackSerializer.Deserialize<DatasetWorkerControlFrame>(payload)
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
            || frame.Detail.Length > 4096
            || frame.BootstrapToken.Length != 64)
            throw new InvalidDataException("Worker control frame identity or bounds are invalid.");
    }
}
