using System.Buffers.Binary;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

[MessagePackObject]
public sealed record DatasetWorkerControlFrame
{
    public const int CurrentProtocolMajor = 4;
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
    [Key(22)] public Remote.MarketDataApiCall? ApiCall { get; init; }
    [Key(23)] public byte[]? ApiResult { get; init; }
    [Key(24)] public string? ApiError { get; init; }
}
