using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatasetWorkerProcessSnapshot
{
    public required string Dataset { get; init; }
    public required Guid WorkerInstanceId { get; init; }
    public required Guid GenerationId { get; init; }
    public required int ProcessId { get; init; }
    public required DateTime StartedOnUtc { get; init; }
    public required bool Running { get; init; }
    public required bool Healthy { get; init; }
    public required bool GracefulStopSucceeded { get; init; }
    public required bool ForcedTermination { get; init; }
    public int? ExitCode { get; init; }
    public string Detail { get; init; } = string.Empty;
    public long ManifestRevision { get; init; }
    public string ManifestFingerprint { get; init; } = string.Empty;
    public DatasetWorkerDiagnostics? Diagnostics { get; init; }
    public bool ControlResponsive { get; init; }
    public bool DataPlaneHealthy { get; init; }
}
