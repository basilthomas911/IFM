using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Worker;

if (!DatasetWorkerArguments.TryParse(args, out var worker, out var error))
{
    Console.Error.WriteLine(error);
    return 2;
}
var bootstrapToken = Environment.GetEnvironmentVariable("IFM_DATASET_WORKER_BOOTSTRAP");
if (bootstrapToken is null || bootstrapToken.Length != 64)
    return 7;

if (OperatingSystem.IsLinux() && NativeMethods.setpgid(0, 0) != 0)
    throw new InvalidOperationException("The dataset worker could not establish its owned process group.");

using var input = new AnonymousPipeClientStream(PipeDirection.In, worker.ControlIn);
using var output = new AnonymousPipeClientStream(PipeDirection.Out, worker.ControlOut);
using var publications = new AnonymousPipeClientStream(PipeDirection.Out, worker.PublicationOut);
var sequence = 0L;
var supervisorSequence = 0L;
var generation = worker.GenerationId;
DatasetSubscriptionManifest? currentManifest = null;
DatasetWorkerRuntime? datasetRuntime = null;
PipeDatasetWorkerPublisher? pipePublisher = null;
using var stopping = new CancellationTokenSource();

await WriteAsync(DatasetWorkerMessageKind.WorkerHello, healthy: false,
    "Dataset worker control host started.", Guid.NewGuid());
var supervisor = await DatasetWorkerFrameCodec.ReadAsync(input, 256 * 1024, stopping.Token);
if (supervisor.Kind != DatasetWorkerMessageKind.SupervisorHello
    || supervisor.WorkerInstanceId != worker.WorkerInstanceId
    || supervisor.Dataset != worker.Dataset
    || supervisor.ValueDate != worker.ValueDate
    || supervisor.GenerationId != generation
    || !ValidSupervisorFrame(supervisor))
    return 3;

// Read independently of native startup/reset so losing the supervisor cancels
// cooperative work even while a command is still executing. A bounded channel
// prevents a malfunctioning supervisor from creating an unbounded command queue.
var commands = Channel.CreateBounded<DatasetWorkerControlFrame>(new BoundedChannelOptions(8)
{
    SingleReader = true,
    SingleWriter = true,
    FullMode = BoundedChannelFullMode.Wait
});
var commandReader = ReadCommandsAsync();
try
{
    while (!stopping.IsCancellationRequested)
    {
        var command = await commands.Reader.ReadAsync(stopping.Token);
        if (command.WorkerInstanceId != worker.WorkerInstanceId
            || command.Dataset != worker.Dataset
            || command.ValueDate != worker.ValueDate
            || command.GenerationId != generation
            || !ValidSupervisorFrame(command))
            return 4;

        if (currentManifest is null && command.Kind != DatasetWorkerMessageKind.StartManifest
            && command.Kind != DatasetWorkerMessageKind.GracefulStop)
        {
            await WriteAsync(DatasetWorkerMessageKind.ProtocolError, false,
                "StartManifest must be accepted before dataset commands.", command.CorrelationId);
            return 6;
        }

        switch (command.Kind)
        {
            case DatasetWorkerMessageKind.StartManifest:
            case DatasetWorkerMessageKind.ApplySubscriptionManifest:
            case DatasetWorkerMessageKind.CooperativeReset:
                if (!TryValidateManifest(command, out var manifestError))
                {
                    await WriteAsync(DatasetWorkerMessageKind.ManifestRejected, false,
                        manifestError, command.CorrelationId);
                    break;
                }
                var manifest = command.Manifest!;
                var acknowledgement = command.Kind switch
                {
                    DatasetWorkerMessageKind.StartManifest => DatasetWorkerMessageKind.StartAccepted,
                    DatasetWorkerMessageKind.CooperativeReset => DatasetWorkerMessageKind.ResetCompleted,
                    _ => DatasetWorkerMessageKind.SubscriptionManifestApplied
                };
                try
                {
                    // Duplicate application is idempotent, but an explicit reset
                    // must reconstruct even if the desired revision is unchanged.
                    if (currentManifest?.Revision != manifest.Revision
                        || command.Kind == DatasetWorkerMessageKind.CooperativeReset)
                        await InstallManifestAsync(manifest);
                    var healthy = datasetRuntime?.IsHealthy == true;
                    await WriteAsync(healthy ? acknowledgement : DatasetWorkerMessageKind.ManifestRejected,
                        healthy, datasetRuntime?.Detail ?? "Dataset runtime is unavailable.",
                        command.CorrelationId);
                }
                catch (Exception exception) when (!stopping.IsCancellationRequested)
                {
                    await WriteAsync(DatasetWorkerMessageKind.ManifestRejected, false,
                        $"Dataset reconstruction failed: {exception.GetType().Name}: {exception.Message}",
                        command.CorrelationId);
                    // Do not serve a partially rebuilt generation. The supervisor
                    // owns the decision to replace this failed dataset process.
                    return 8;
                }
                break;
            case DatasetWorkerMessageKind.HealthSnapshot:
                await WriteAsync(DatasetWorkerMessageKind.HealthSnapshot,
                    datasetRuntime?.IsHealthy == true,
                    datasetRuntime?.Detail ?? "Dataset runtime is unavailable.", command.CorrelationId);
                break;
            case DatasetWorkerMessageKind.GracefulStop:
                await StopRuntimeAsync();
                await WriteAsync(DatasetWorkerMessageKind.Stopped, false,
                    "Dataset worker stopped gracefully.", command.CorrelationId);
                return 0;
            case DatasetWorkerMessageKind.Hang:
                await Task.Delay(Timeout.InfiniteTimeSpan, stopping.Token);
                return 5;
            default:
                await WriteAsync(DatasetWorkerMessageKind.ProtocolError, false,
                    $"Unsupported command {command.Kind}.", command.CorrelationId);
                return 6;
        }
    }
}
catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
catch (ChannelClosedException) { }
finally
{
    await stopping.CancelAsync();
    try { await commandReader; }
    finally { await StopRuntimeAsync(); }
}

return 0;

async Task ReadCommandsAsync()
{
    try
    {
        while (!stopping.IsCancellationRequested)
        {
            var command = await DatasetWorkerFrameCodec.ReadAsync(input, 256 * 1024, stopping.Token);
            await commands.Writer.WriteAsync(command, stopping.Token);
        }
    }
    catch (Exception exception) when (exception is IOException or OperationCanceledException
        or InvalidDataException)
    {
        commands.Writer.TryComplete(exception);
    }
    finally
    {
        commands.Writer.TryComplete();
        await stopping.CancelAsync();
    }
}

bool TryValidateManifest(DatasetWorkerControlFrame command, out string error)
{
    try
    {
        var manifest = command.Manifest
            ?? throw new InvalidDataException("A complete dataset subscription manifest is required.");
        manifest.Validate();
        if (manifest.Dataset != worker.Dataset || manifest.ValueDate != worker.ValueDate
            || manifest.Revision != command.ManifestRevision
            || manifest.Fingerprint != command.ManifestFingerprint)
            throw new InvalidDataException("Manifest identity, revision or fingerprint does not match the command.");
        if (currentManifest is not null
            && (manifest.Revision < currentManifest.Revision
                || manifest.Revision == currentManifest.Revision
                    && manifest.Fingerprint != currentManifest.Fingerprint))
            throw new InvalidDataException("Manifest revision is stale or conflicts with its accepted contents.");
        if (currentManifest is not null && command.Kind == DatasetWorkerMessageKind.StartManifest
            && manifest.Revision != currentManifest.Revision)
            throw new InvalidDataException("An already started worker requires ApplySubscriptionManifest.");
        error = string.Empty;
        return true;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException
        or InvalidOperationException)
    {
        error = exception.Message;
        return false;
    }
}

async Task InstallManifestAsync(DatasetSubscriptionManifest manifest)
{
    await StopRuntimeAsync();
    pipePublisher = new PipeDatasetWorkerPublisher(publications, worker.Dataset,
        worker.ValueDate, worker.WorkerInstanceId, manifest.Revision);
    datasetRuntime = await DatasetWorkerRuntime.StartAsync(manifest, worker.DeploymentProfile,
        worker.DataSource, worker.Synthetic, pipePublisher, stopping.Token);
    if (!datasetRuntime.IsHealthy || datasetRuntime.GenerationId == Guid.Empty)
        throw new InvalidOperationException("The replacement dataset generation is not healthy.");
    generation = datasetRuntime.GenerationId;
    await pipePublisher.BindGenerationAsync(generation, stopping.Token);
    currentManifest = manifest;
}

async Task StopRuntimeAsync()
{
    if (pipePublisher is not null)
        await pipePublisher.CloseAsync(CancellationToken.None);
    if (datasetRuntime is not null)
    {
        await datasetRuntime.DisposeAsync();
        datasetRuntime = null;
    }
    if (pipePublisher is not null)
    {
        await pipePublisher.DisposeAsync();
        pipePublisher = null;
    }
}

async ValueTask WriteAsync(
    DatasetWorkerMessageKind kind,
    bool healthy,
    string detail,
    Guid correlationId)
{
    // A failed reconstruction may own an unaccepted epoch while the control identity
    // still names its predecessor. Do not attach cross-generation diagnostics.
    var diagnostics = datasetRuntime is { } activeRuntime && activeRuntime.GenerationId == generation
        ? activeRuntime.GetDiagnostics() : null;
    await DatasetWorkerFrameCodec.WriteAsync(output, new()
    {
        Kind = kind,
        WorkerInstanceId = worker.WorkerInstanceId,
        Dataset = worker.Dataset,
        ValueDate = worker.ValueDate,
        GenerationId = generation,
        CorrelationId = correlationId,
        Sequence = Interlocked.Increment(ref sequence),
        ProcessId = Environment.ProcessId,
        Healthy = healthy && diagnostics?.Operational == true,
        Detail = detail.Length <= 4096 ? detail : detail[..4096],
        ManifestRevision = currentManifest?.Revision ?? 0,
        ManifestFingerprint = currentManifest?.Fingerprint ?? string.Empty,
        Diagnostics = diagnostics,
        BootstrapToken = bootstrapToken
    }, 256 * 1024, stopping.Token);
}

bool ValidSupervisorFrame(DatasetWorkerControlFrame frame)
{
    if (frame.Sequence <= supervisorSequence
        || !CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(frame.BootstrapToken),
            Encoding.ASCII.GetBytes(bootstrapToken)))
        return false;
    supervisorSequence = frame.Sequence;
    return true;
}

file sealed record DatasetWorkerArguments(
    string ControlIn,
    string ControlOut,
    string PublicationOut,
    string Dataset,
    DateOnly ValueDate,
    Guid WorkerInstanceId,
    Guid GenerationId,
    TomasAI.IFM.Framework.MarketData.DataBento.FeedDeploymentProfile DeploymentProfile,
    TomasAI.IFM.Framework.MarketData.DataBento.FeedDataSourceMode DataSource,
    TomasAI.IFM.Framework.MarketData.DataBento.SyntheticFeedOptions Synthetic)
{
    public static bool TryParse(string[] values, out DatasetWorkerArguments result, out string error)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index + 1 < values.Length; index += 2)
            map[values[index]] = values[index + 1];
        string? input = null, output = null, publication = null, dataset = null;
        var valueDate = default(DateOnly);
        var workerId = Guid.Empty;
        var generationId = Guid.Empty;
        var valid = map.TryGetValue("--control-in", out input)
            && map.TryGetValue("--control-out", out output)
            && map.TryGetValue("--publication-out", out publication)
            && map.TryGetValue("--dataset", out dataset)
            && map.TryGetValue("--value-date", out var valueDateText)
            && DateOnly.TryParseExact(valueDateText, "yyyy-MM-dd", out valueDate)
            && map.TryGetValue("--worker-id", out var workerText)
            && Guid.TryParse(workerText, out workerId)
            && map.TryGetValue("--generation-id", out var generationText)
            && Guid.TryParse(generationText, out generationId)
            && !string.IsNullOrWhiteSpace(dataset)
            && workerId != Guid.Empty
            && generationId != Guid.Empty;
        if (!valid)
        {
            result = null!;
            error = "Dataset worker bootstrap arguments are invalid.";
            return false;
        }
        var profile = ParseEnum(map, "--deployment-profile",
            TomasAI.IFM.Framework.MarketData.DataBento.FeedDeploymentProfile.SyntheticCi);
        var dataSource = ParseEnum(map, "--data-source",
            TomasAI.IFM.Framework.MarketData.DataBento.FeedDataSourceMode.Synthetic);
        var synthetic = new TomasAI.IFM.Framework.MarketData.DataBento.SyntheticFeedOptions
        {
            RecordCount = ParseInt(map, "--synthetic-record-count", 1_000_000),
            RecordsPerSecond = ParseInt(map, "--synthetic-records-per-second", 100),
            StartSequence = ParseUlong(map, "--synthetic-start-sequence", 1)
        };
        result = new(input!, output!, publication!, dataset!, valueDate, workerId, generationId,
            profile, dataSource, synthetic);
        error = string.Empty;
        return true;
    }

    static T ParseEnum<T>(IReadOnlyDictionary<string, string> values, string key, T fallback)
        where T : struct, Enum => values.TryGetValue(key, out var text)
            && Enum.TryParse<T>(text, true, out var value) ? value : fallback;
    static int ParseInt(IReadOnlyDictionary<string, string> values, string key, int fallback) =>
        values.TryGetValue(key, out var text) && int.TryParse(text, out var value) ? value : fallback;
    static ulong ParseUlong(IReadOnlyDictionary<string, string> values, string key, ulong fallback) =>
        values.TryGetValue(key, out var text) && ulong.TryParse(text, out var value) ? value : fallback;
}

file static class NativeMethods
{
    [DllImport("libc", EntryPoint = "setpgid")]
    internal static extern int setpgid(int processId, int processGroupId);
}
