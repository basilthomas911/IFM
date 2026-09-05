using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.QualificationHost;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;

// Deliberately separate from the production worker. This helper proves real OS process-tree
// containment; its protocol-only child does not constitute native-feed qualification evidence.
if (args.Length == 0) return 2;
if (args[0] == "--leaf")
{
    using var ignoreTermination = OperatingSystem.IsLinux()
        ? PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => context.Cancel = true) : null;
    Console.WriteLine("ready");
    Console.Out.Flush();
    await Task.Delay(TimeSpan.FromMinutes(2));
    return 0;
}
if (args[0] == "--parent")
{
    await using var owner = new DatasetWorkerProcessSupervisor(new DatabentoStage3Options
    {
        WorkerHandshakeTimeout = TimeSpan.FromSeconds(10),
        WorkerStartTimeout = TimeSpan.FromSeconds(10),
        WorkerGracefulStopTimeout = TimeSpan.FromMilliseconds(200),
        WorkerForceKillTimeout = TimeSpan.FromSeconds(5)
    });
    var manifest = new DatasetDesiredSubscriptionRegistry().Set("GLBX.MDP3", new DateOnly(2026, 9, 4),
        [new DatabentoContractRegistration
        {
            DomainContractId = "ES20261218", ProviderContractName = "ESZ6", Dataset = "GLBX.MDP3",
            RootSymbol = "ES", AssetTypeId = AssetTypeId.Futures, OnTheRun = true, Rollover = true
        }]);
    var started = await owner.StartAsync(new DatasetWorkerStartRequest
    {
        ExecutablePath = Environment.ProcessPath!,
        PrefixArguments = [typeof(QualificationHostMarker).Assembly.Location, "--worker", "true"],
        Dataset = manifest.Dataset, ValueDate = manifest.ValueDate, GenerationId = Guid.NewGuid(),
        WorkerInstanceId = Guid.NewGuid(), Manifest = manifest, ManifestRevision = manifest.Revision
    });
    Console.WriteLine(JsonSerializer.Serialize(new QualificationProcessTree(
        Environment.ProcessId, started.ProcessId, int.Parse(started.Detail))));
    Console.Out.Flush();
    await Task.Delay(TimeSpan.FromMinutes(2));
    return 0;
}
if (args[0] != "--worker") return 3;
var values = new Dictionary<string, string>(StringComparer.Ordinal);
for (var index = 0; index + 1 < args.Length; index += 2) values.Add(args[index], args[index + 1]);
if (OperatingSystem.IsLinux() && Native.setpgid(0, 0) != 0) return 4;
using var input = new AnonymousPipeClientStream(PipeDirection.In, values["--control-in"]);
using var output = new AnonymousPipeClientStream(PipeDirection.Out, values["--control-out"]);
using var publication = new AnonymousPipeClientStream(PipeDirection.Out, values["--publication-out"]);
var token = Environment.GetEnvironmentVariable("IFM_DATASET_WORKER_BOOTSTRAP")!;
var dataset = values["--dataset"];
var date = DateOnly.Parse(values["--value-date"]);
var workerId = Guid.Parse(values["--worker-id"]);
var generation = Guid.Parse(values["--generation-id"]);
long sequence = 0;
DatasetSubscriptionManifest? current = null;
Process? descendant = null;
await WriteAsync(DatasetWorkerMessageKind.WorkerHello, Guid.NewGuid(), false);
_ = await DatasetWorkerFrameCodec.ReadAsync(input, 256 * 1024, CancellationToken.None);
try
{
    while (true)
    {
        var command = await DatasetWorkerFrameCodec.ReadAsync(input, 256 * 1024, CancellationToken.None);
        switch (command.Kind)
        {
            case DatasetWorkerMessageKind.StartManifest:
                current = command.Manifest!;
                generation = Guid.NewGuid();
                var launch = new ProcessStartInfo(Environment.ProcessPath!)
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                launch.ArgumentList.Add(typeof(QualificationHostMarker).Assembly.Location);
                launch.ArgumentList.Add("--leaf");
                descendant = Process.Start(launch)!;
                if (await descendant.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)) != "ready")
                    throw new InvalidOperationException("The containment descendant did not become ready.");
                await WriteAsync(DatasetWorkerMessageKind.StartAccepted, command.CorrelationId, true);
                break;
            case DatasetWorkerMessageKind.HealthSnapshot:
                await WriteAsync(DatasetWorkerMessageKind.HealthSnapshot, command.CorrelationId, true);
                break;
            case DatasetWorkerMessageKind.CooperativeReset:
            case DatasetWorkerMessageKind.ApplySubscriptionManifest:
                current = command.Manifest!;
                generation = Guid.NewGuid();
                await WriteAsync(command.Kind == DatasetWorkerMessageKind.CooperativeReset
                    ? DatasetWorkerMessageKind.ResetCompleted : DatasetWorkerMessageKind.SubscriptionManifestApplied,
                    command.CorrelationId, true);
                break;
            case DatasetWorkerMessageKind.GracefulStop:
                await WriteAsync(DatasetWorkerMessageKind.Stopped, command.CorrelationId, false);
                return 0;
            case DatasetWorkerMessageKind.Hang:
                await Task.Delay(TimeSpan.FromMinutes(2));
                return 0;
            default: return 5;
        }
    }
}
catch (EndOfStreamException) { return 0; }
finally { descendant?.Dispose(); }

ValueTask WriteAsync(DatasetWorkerMessageKind kind, Guid correlation, bool healthy) =>
    DatasetWorkerFrameCodec.WriteAsync(output, new DatasetWorkerControlFrame
    {
        Kind = kind, WorkerInstanceId = workerId, Dataset = dataset, ValueDate = date,
        GenerationId = generation, CorrelationId = correlation, Sequence = ++sequence,
        ProcessId = Environment.ProcessId, Healthy = healthy, Detail = descendant?.Id.ToString() ?? "0",
        BootstrapToken = token, ManifestRevision = current?.Revision ?? 0,
        ManifestFingerprint = current?.Fingerprint ?? string.Empty
    }, 256 * 1024, CancellationToken.None);

file static class Native
{
    [DllImport("libc", SetLastError = true)]
    internal static extern int setpgid(int processId, int processGroupId);
}

namespace TomasAI.IFM.Application.MarketData.QualificationHost
{
    public sealed class QualificationHostMarker;
    public sealed record QualificationProcessTree(int ParentId, int WorkerId, int DescendantId);
}
