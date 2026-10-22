using System.Buffers.Binary;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "The serialized message enum has a fixed underlying width; preserve its wire representation.")]
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
    ApplyOptionChainOwnership = 22,
    MarketDataApiCall = 23,
    MarketDataApiResult = 24
}
