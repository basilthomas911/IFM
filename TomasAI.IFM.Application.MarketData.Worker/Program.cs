using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
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

if (OperatingSystem.IsLinux())
    _ = NativeMethods.setpgid(0, 0);

using var input = new AnonymousPipeClientStream(PipeDirection.In, worker.ControlIn);
using var output = new AnonymousPipeClientStream(PipeDirection.Out, worker.ControlOut);
using var publications = new AnonymousPipeClientStream(PipeDirection.Out, worker.PublicationOut);
var sequence = 0L;
var supervisorSequence = 0L;
var generation = worker.GenerationId;
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

if (worker.Contracts.Count == 0)
{
    await WriteAsync(DatasetWorkerMessageKind.ProtocolError, healthy: false,
        "A dataset contract manifest is required by this worker build.", Guid.NewGuid());
    return 5;
}

await using var pipePublisher = new PipeDatasetWorkerPublisher(publications, worker.Dataset,
    worker.ValueDate, worker.WorkerInstanceId, generation);
await using var datasetRuntime = await DatasetWorkerRuntime.StartAsync(
    worker.Dataset, worker.Contracts, worker.ValueDate, worker.DeploymentProfile,
    worker.DataSource, worker.Synthetic, pipePublisher, stopping.Token);
await WriteAsync(DatasetWorkerMessageKind.WorkerReady, datasetRuntime.IsHealthy,
    datasetRuntime.Detail, Guid.NewGuid());

while (!stopping.IsCancellationRequested)
{
    DatasetWorkerControlFrame command;
    try
    {
        command = await DatasetWorkerFrameCodec.ReadAsync(input, 256 * 1024, stopping.Token);
    }
    catch (EndOfStreamException)
    {
        return 0;
    }
    if (command.WorkerInstanceId != worker.WorkerInstanceId
        || command.Dataset != worker.Dataset
        || command.ValueDate != worker.ValueDate
        || command.GenerationId != generation
        || !ValidSupervisorFrame(command))
        return 4;

    switch (command.Kind)
    {
        case DatasetWorkerMessageKind.HealthSnapshot:
            await WriteAsync(DatasetWorkerMessageKind.HealthSnapshot,
                datasetRuntime.IsHealthy, datasetRuntime.Detail, command.CorrelationId);
            break;
        case DatasetWorkerMessageKind.CooperativeReset:
            generation = await datasetRuntime.ResetAsync(stopping.Token);
            pipePublisher.ChangeGeneration(generation);
            await WriteAsync(DatasetWorkerMessageKind.ResetCompleted,
                datasetRuntime.IsHealthy, datasetRuntime.Detail,
                command.CorrelationId);
            break;
        case DatasetWorkerMessageKind.GracefulStop:
            await datasetRuntime.DisposeAsync();
            await WriteAsync(DatasetWorkerMessageKind.Stopped, false,
                "Dataset worker stopped gracefully.", command.CorrelationId);
            return 0;
        case DatasetWorkerMessageKind.Hang:
            await Task.Delay(Timeout.InfiniteTimeSpan);
            return 5;
        default:
            await WriteAsync(DatasetWorkerMessageKind.ProtocolError, false,
                $"Unsupported command {command.Kind}.", command.CorrelationId);
            return 6;
    }
}

return 0;

async ValueTask WriteAsync(
    DatasetWorkerMessageKind kind,
    bool healthy,
    string detail,
    Guid correlationId)
{
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
        Healthy = healthy,
        Detail = detail,
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
    IReadOnlyList<string> Contracts,
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
        map.TryGetValue("--contracts", out var contracts);
        if (contracts is null && map.TryGetValue("--synthetic-contracts", out var syntheticContracts))
            contracts = syntheticContracts;
        if (contracts is null && map.TryGetValue("--synthetic-contract", out var legacyContract))
            contracts = legacyContract;
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
            string.IsNullOrWhiteSpace(contracts)
                ? [] : contracts.Split('|', StringSplitOptions.RemoveEmptyEntries),
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
