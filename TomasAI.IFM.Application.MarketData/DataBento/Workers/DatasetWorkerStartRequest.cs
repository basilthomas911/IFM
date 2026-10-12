using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record DatasetWorkerStartRequest
{
    public required string ExecutablePath { get; init; }
    public IReadOnlyList<string> PrefixArguments { get; init; } = [];
    public required string Dataset { get; init; }
    public required DateOnly ValueDate { get; init; }
    public required Guid WorkerInstanceId { get; init; }
    public required Guid GenerationId { get; init; }
    public long ManifestRevision { get; init; } = 1;
    public DatasetSubscriptionManifest? Manifest { get; init; }
}
