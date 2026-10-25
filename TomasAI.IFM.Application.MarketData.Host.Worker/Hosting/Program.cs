using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Host.Worker;
using TomasAI.IFM.Application.MarketData.Pricing;

using Serilog;

namespace TomasAI.IFM.Application.MarketData.Host.Worker;
internal static class Program
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Process entry point reports a redacted fatal exception and returns a failure exit code.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6667", Justification = "All worker exception text is explicitly redacted before it reaches file or collector logs.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Entry point includes local worker orchestration functions with one process-wide logging lifetime.")]
    public static async Task<int> Main(string[] args)
    {
Log.Logger = DatasetWorkerLogging.CreateLogger();
using var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger, dispose: false);
try
{
    var exitCode = await (RunWorkerAsync()).ConfigureAwait(false);
    Log.Information("{Component}.{Method} exited with {ExitCode}", "DatasetWorker", "RunWorkerAsync", exitCode);
    return exitCode;
}
catch (Exception exception)
{
    Log.Fatal("{Component}.{Method} failed: {Failure}", "DatasetWorker", "RunWorkerAsync", DatasetWorkerLogging.Redact(exception.ToString()));
    return 1;
}
finally
{
    await (Log.CloseAndFlushAsync()).ConfigureAwait(false);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S6667", Justification = "Worker errors are redacted before logging; raw exceptions could expose inherited credentials.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Owns the ordered control loop and associated local pipe/runtime cleanup functions.")]
async Task<int> RunWorkerAsync()
{
    if (!DatasetWorkerArguments.TryParse(args, out var worker, out var error))
    {
        Log.Error("{Component}.{Method} invalid arguments: {Failure}", "DatasetWorker", "TryParse", DatasetWorkerLogging.Redact(error));
        return 2;
    }
    using var datasetContext = Serilog.Context.LogContext.PushProperty("Dataset", worker.Dataset);
    using var workerContext = Serilog.Context.LogContext.PushProperty("WorkerInstanceId", worker.WorkerInstanceId);
    using var generationContext = Serilog.Context.LogContext.PushProperty("BootstrapGenerationId", worker.GenerationId);
    using var valueDateContext = Serilog.Context.LogContext.PushProperty("ValueDate", worker.ValueDate.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Log.Information("{Component}.{Method} starting with {DeploymentProfile} and {DataSource}",
        "DatasetWorker", "RunWorkerAsync", worker.DeploymentProfile, worker.DataSource);
    var bootstrapToken = Environment.GetEnvironmentVariable("IFM_DATASET_WORKER_BOOTSTRAP");
    if (bootstrapToken is null || bootstrapToken.Length != 64)
    {
        Log.Error("{Component}.{Method} bootstrap token is missing or invalid", "DatasetWorker", "RunWorkerAsync");
        return 7;
    }

    if (OperatingSystem.IsLinux() && NativeMethods.setpgid(0, 0) != 0)
        throw new InvalidOperationException("The dataset worker could not establish its owned process group.");

    using var input = new AnonymousPipeClientStream(PipeDirection.In, worker.ControlIn);
    using var output = new AnonymousPipeClientStream(PipeDirection.Out, worker.ControlOut);
    using var publications = new AnonymousPipeClientStream(PipeDirection.Out, worker.PublicationOut);
    using var retentionAcknowledgments = new AnonymousPipeClientStream(PipeDirection.In, worker.RetentionAckIn);
    var sequence = 0L;
    var supervisorSequence = 0L;
    var generation = worker.GenerationId;
    DatasetSubscriptionManifest? currentManifest = null;
    DatasetWorkerRuntime? datasetRuntime = null;
    TomasAI.IFM.Framework.MarketData.DataBento.DatabentoDiagnostics.Install(diagnostic =>
    {
        var level = diagnostic.Kind != 2 ? Serilog.Events.LogEventLevel.Error
            : diagnostic.Code == 2 || diagnostic.Code == 255 ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Information;
        Log.Write(level, "Databento provider diagnostic; Dataset={Dataset}; WorkerInstanceId={WorkerInstanceId}; GenerationId={GenerationId}; Kind={Kind}; Code={Code}; CodeName={CodeName}; SessionFatal={SessionFatal}; IsLast={IsLast}; Detail={Detail}",
            worker.Dataset, worker.WorkerInstanceId, generation, diagnostic.Kind, diagnostic.Code, diagnostic.CodeName, diagnostic.SessionFatal, diagnostic.IsLast, diagnostic.Message);
    });
    PipeDatasetWorkerPublisher? pipePublisher = null;
    using var stopping = new CancellationTokenSource();

    await (WriteAsync(DatasetWorkerMessageKind.WorkerHello, healthy: false,
        "Dataset worker control host started.", Guid.NewGuid())).ConfigureAwait(false);
    var supervisor = await (DatasetWorkerFrameCodec.ReadAsync(input, 4 * 1024 * 1024, stopping.Token)).ConfigureAwait(false);
    if (supervisor.Kind != DatasetWorkerMessageKind.SupervisorHello
        || supervisor.WorkerInstanceId != worker.WorkerInstanceId
        || !string.Equals(supervisor.Dataset,worker.Dataset,StringComparison.Ordinal)
        || supervisor.ValueDate != worker.ValueDate
        || supervisor.GenerationId != generation
        || !ValidSupervisorFrame(supervisor))
    {
        Log.Error("{Component}.{Method} supervisor handshake rejected; Kind={Kind}; WorkerMatches={WorkerMatches}; DatasetMatches={DatasetMatches}; ValueDateMatches={ValueDateMatches}; GenerationMatches={GenerationMatches}; Sequence={Sequence}; PreviousSequence={PreviousSequence}; TokenMatches={TokenMatches}",
            "DatasetWorker", "RunWorkerAsync", supervisor.Kind,
            supervisor.WorkerInstanceId == worker.WorkerInstanceId, string.Equals(supervisor.Dataset,worker.Dataset,StringComparison.Ordinal),
            supervisor.ValueDate == worker.ValueDate, supervisor.GenerationId == generation,
            supervisor.Sequence, supervisorSequence,
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supervisor.BootstrapToken), Encoding.ASCII.GetBytes(bootstrapToken)));
        return 3;
    }

    // Read independently of native startup/reset so losing the supervisor cancels
    // cooperative work even while a command is still executing. A bounded channel
    // prevents a malfunctioning supervisor from creating an unbounded command queue.
    var commands = Channel.CreateBounded<DatasetWorkerControlFrame>(new BoundedChannelOptions(8)
    {
        SingleReader = true,
        SingleWriter = true,
        FullMode = BoundedChannelFullMode.Wait
    });
    long commandStarted = 0;
    var commandReader = ReadCommandsAsync();
    var healthReader = ReadHealthAsync();
    try
    {
        while (!stopping.IsCancellationRequested)
        {
            var command = await (commands.Reader.ReadAsync(stopping.Token)).ConfigureAwait(false);
            if (command.WorkerInstanceId != worker.WorkerInstanceId
                || !string.Equals(command.Dataset,worker.Dataset,StringComparison.Ordinal)
                || command.ValueDate != worker.ValueDate
                || command.GenerationId != generation
                || !ValidSupervisorFrame(command))
                return 4;

            if (currentManifest is null && command.Kind != DatasetWorkerMessageKind.StartManifest
                && command.Kind != DatasetWorkerMessageKind.GracefulStop)
            {
                await (WriteAsync(DatasetWorkerMessageKind.ProtocolError, false,
                    "StartManifest must be accepted before dataset commands.", command.CorrelationId)).ConfigureAwait(false);
                return 6;
            }

            Volatile.Write(ref commandStarted, Environment.TickCount64);
            switch (command.Kind)
            {
                case DatasetWorkerMessageKind.MarketDataApiCall:
                    try
                    {
                        var result = await datasetRuntime!.InvokeMarketDataApiAsync(command.ApiCall!, stopping.Token).ConfigureAwait(false);
                        await WriteAsync(DatasetWorkerMessageKind.MarketDataApiResult, datasetRuntime.IsHealthy,
                            "Market-data API operation completed.", command.CorrelationId, apiResult: result).ConfigureAwait(false);
                    }
                    catch (Exception apiFailure) when (!stopping.IsCancellationRequested)
                    {
                        datasetRuntime?.MarkFailed(apiFailure);
                        Log.Error("Worker API processing failed; CorrelationId={CorrelationId}; Failure={Failure}", command.CorrelationId, DatasetWorkerLogging.Redact(apiFailure.ToString()));
                        await WriteAsync(DatasetWorkerMessageKind.MarketDataApiResult, datasetRuntime?.IsHealthy == true,
                            "Market-data API operation failed.", command.CorrelationId, apiError: DatasetWorkerLogging.Redact(apiFailure.Message)).ConfigureAwait(false);
                    }
                    break;
                case DatasetWorkerMessageKind.AcquireOptionChain:
                case DatasetWorkerMessageKind.ReleaseOptionChain:
                case DatasetWorkerMessageKind.ApplyOptionChainOwnership:
                case DatasetWorkerMessageKind.CaptureCompositionSnapshot:
                    try
                    {
                        if (command.Kind == DatasetWorkerMessageKind.CaptureCompositionSnapshot)
                        {
                            var captured = await (datasetRuntime!.CaptureCompositionSnapshotAsync(command.CompositionRequest!, stopping.Token)).ConfigureAwait(false);
                            await (WriteAsync(DatasetWorkerMessageKind.CompositionSnapshotResult, datasetRuntime.IsHealthy,
                                "Composition snapshot evaluated.", command.CorrelationId, compositionResult: captured)).ConfigureAwait(false);
                        }
                        else
                        {
                            var result = command.Kind == DatasetWorkerMessageKind.AcquireOptionChain
                                ? await (datasetRuntime!.AcquireOptionChainAsync(command.OptionChain!, stopping.Token)).ConfigureAwait(false)
                                : await (datasetRuntime!.ReleaseOptionChainAsync(command.OptionChainRelease!, stopping.Token)).ConfigureAwait(false);
                            await (WriteAsync(DatasetWorkerMessageKind.OptionChainResult, datasetRuntime.IsHealthy,
                                "Option chain operation evaluated.", command.CorrelationId, chainResult: result)).ConfigureAwait(false);
                        }
                    }
                    catch (Exception exception) when (!stopping.IsCancellationRequested)
                    {
                        datasetRuntime?.MarkFailed(exception);
                        Log.Error("{Component}.{Method} control {CommandKind} failed for {CorrelationId}: {Failure}",
                            "DatasetWorker", "RunWorkerAsync", command.Kind, command.CorrelationId, DatasetWorkerLogging.Redact(exception.ToString()));
                        var detail = exception.GetType().Name + ": " + exception.Message;
                        var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY");
                        if (!string.IsNullOrEmpty(apiKey)) detail = detail.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
                        var failure = new TomasAI.IFM.Shared.MarketData.Contracts.Pricing.OptionPricingFailure(
                            "WorkerOperationFailed", "Chain", "", detail[..Math.Min(detail.Length, 512)]);
                        await (WriteAsync(command.Kind == DatasetWorkerMessageKind.CaptureCompositionSnapshot
                            ? DatasetWorkerMessageKind.CompositionSnapshotResult : DatasetWorkerMessageKind.OptionChainResult,
                            datasetRuntime?.IsHealthy == true, "Option operation failed.", command.CorrelationId,
                            chainResult: command.Kind == DatasetWorkerMessageKind.CaptureCompositionSnapshot ? null : new(false, failure),
                            compositionResult: command.Kind == DatasetWorkerMessageKind.CaptureCompositionSnapshot ? new(null, failure) : null)).ConfigureAwait(false);
                    }
                    break;
                case DatasetWorkerMessageKind.StartManifest:
                case DatasetWorkerMessageKind.ApplySubscriptionManifest:
                case DatasetWorkerMessageKind.CooperativeReset:
                    if (!TryValidateManifest(command, out var manifestError))
                    {
                        await (WriteAsync(DatasetWorkerMessageKind.ManifestRejected, false,
                            manifestError, command.CorrelationId)).ConfigureAwait(false);
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
                            await (InstallManifestAsync(manifest)).ConfigureAwait(false);
                        var healthy = datasetRuntime?.IsHealthy == true;
                        await (WriteAsync(healthy ? acknowledgement : DatasetWorkerMessageKind.ManifestRejected,
                            healthy, datasetRuntime?.Detail ?? "Dataset runtime is unavailable.",
                            command.CorrelationId)).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (!stopping.IsCancellationRequested)
                    {
                        Log.Error("{Component}.{Method} control {CommandKind} failed for {CorrelationId}: {Failure}",
                            "DatasetWorker", "RunWorkerAsync", command.Kind, command.CorrelationId, DatasetWorkerLogging.Redact(exception.ToString()));
                        await (WriteAsync(DatasetWorkerMessageKind.ManifestRejected, false,
                            $"Dataset reconstruction failed: {exception.GetType().Name}: {exception.Message}",
                            command.CorrelationId)).ConfigureAwait(false);
                        // Do not serve a partially rebuilt generation. The supervisor
                        // owns the decision to replace this failed dataset process.
                        return 8;
                    }
                    break;
                case DatasetWorkerMessageKind.HealthSnapshot:
                    await (WriteAsync(DatasetWorkerMessageKind.HealthSnapshot,
                        datasetRuntime?.IsHealthy == true,
                        datasetRuntime?.Detail ?? "Dataset runtime is unavailable.", command.CorrelationId)).ConfigureAwait(false);
                    break;
                case DatasetWorkerMessageKind.GracefulStop:
                    await (StopRuntimeAsync()).ConfigureAwait(false);
                    await (WriteAsync(DatasetWorkerMessageKind.Stopped, false,
                        "Dataset worker stopped gracefully.", command.CorrelationId)).ConfigureAwait(false);
                    return 0;
                case DatasetWorkerMessageKind.Hang:
                    await (Task.Delay(Timeout.InfiniteTimeSpan, stopping.Token)).ConfigureAwait(false);
                    return 5;
                default:
                    await (WriteAsync(DatasetWorkerMessageKind.ProtocolError, false,
                        $"Unsupported command {command.Kind}.", command.CorrelationId)).ConfigureAwait(false);
                    return 6;
            }
            Volatile.Write(ref commandStarted, 0);
        }
    }
    catch (OperationCanceledException) when (stopping.IsCancellationRequested) { /* Normal worker stop. */ }
    catch (ChannelClosedException) { /* Control input ended; runtime shutdown follows in finally. */ }
    finally
    {
        await (stopping.CancelAsync()).ConfigureAwait(false);
        try { await Task.WhenAll(commandReader, healthReader).ConfigureAwait(false); }
        finally { await (StopRuntimeAsync()).ConfigureAwait(false); }
    }

    return 0;

    async Task ReadHealthAsync()
    {
        var healthInIndex = Array.IndexOf(args, "--health-in");
        var healthOutIndex = Array.IndexOf(args, "--health-out");
        if (healthInIndex < 0 || healthOutIndex < 0) return; // Older standalone qualification callers.
        using var healthIn = new AnonymousPipeClientStream(PipeDirection.In, args[healthInIndex + 1]);
        using var healthOut = new AnonymousPipeClientStream(PipeDirection.Out, args[healthOutIndex + 1]);
        long received = 0, sent = 0;
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var request = await DatasetWorkerFrameCodec.ReadAsync(healthIn, 4 * 1024 * 1024, stopping.Token).ConfigureAwait(false);
                if (request.Kind != DatasetWorkerMessageKind.HealthSnapshot || request.WorkerInstanceId != worker.WorkerInstanceId
                    || request.Dataset != worker.Dataset || request.ValueDate != worker.ValueDate
                    || request.BootstrapToken != bootstrapToken || request.Sequence <= received)
                    throw new InvalidDataException("Independent health request identity does not match.");
                received = request.Sequence;
                var runtime = datasetRuntime;
                var diagnostics = runtime?.GetDiagnostics();
                var currentGeneration = generation;
                var started = Volatile.Read(ref commandStarted);
                var stalled = started != 0 && Environment.TickCount64 - started >= 120_000;
                await DatasetWorkerFrameCodec.WriteAsync(healthOut, new()
                {
                    Kind = DatasetWorkerMessageKind.HealthSnapshot, WorkerInstanceId = worker.WorkerInstanceId,
                    Dataset = worker.Dataset, ValueDate = worker.ValueDate, GenerationId = currentGeneration,
                    CorrelationId = request.CorrelationId, Sequence = ++sent, BootstrapToken = bootstrapToken,
                    ProcessId = Environment.ProcessId, Healthy = runtime?.IsHealthy == true && diagnostics?.Operational == true && !stalled,
                    Detail = stalled ? "Business control stalled" : "Independent dataset health", ManifestRevision = currentManifest?.Revision ?? 0,
                    ManifestFingerprint = currentManifest?.Fingerprint ?? string.Empty,
                    Diagnostics = diagnostics?.GenerationId == currentGeneration ? diagnostics : null
                }, 4 * 1024 * 1024, stopping.Token).ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or InvalidDataException)
        {
            if (!stopping.IsCancellationRequested)
                Log.Warning("Independent dataset health channel closed: {Failure}", DatasetWorkerLogging.Redact(error.Message));
        }
    }

    async Task ReadCommandsAsync()
    {
        try
        {
            while (!stopping.IsCancellationRequested)
            {
                var command = await (DatasetWorkerFrameCodec.ReadAsync(input, 4 * 1024 * 1024, stopping.Token)).ConfigureAwait(false);
                await (commands.Writer.WriteAsync(command, stopping.Token)).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException
            or InvalidDataException)
        {
            if (!stopping.IsCancellationRequested && exception is not OperationCanceledException)
                Log.Warning("{Component}.{Method} control input closed: {Failure}",
                    "DatasetWorker", "ReadCommandsAsync", DatasetWorkerLogging.Redact(exception.ToString()));
            commands.Writer.TryComplete(exception);
        }
        finally
        {
            commands.Writer.TryComplete();
            await (stopping.CancelAsync()).ConfigureAwait(false);
        }
    }

    bool TryValidateManifest(DatasetWorkerControlFrame command, out string error)
    {
        try
        {
            var manifest = command.Manifest
                ?? throw new InvalidDataException("A complete dataset subscription manifest is required.");
            manifest.Validate();
            if (!string.Equals(manifest.Dataset,worker.Dataset,StringComparison.Ordinal) || manifest.ValueDate != worker.ValueDate
                || manifest.Revision != command.ManifestRevision
                || !string.Equals(manifest.Fingerprint,command.ManifestFingerprint,StringComparison.Ordinal))
                throw new InvalidDataException("Manifest identity, revision or fingerprint does not match the command.");
            if (currentManifest is not null
                && (manifest.Revision < currentManifest.Revision
                    || manifest.Revision == currentManifest.Revision
                        && !string.Equals(manifest.Fingerprint,currentManifest.Fingerprint,StringComparison.Ordinal)))
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

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2000", Justification = "Publisher ownership remains with worker orchestration: failed start disposes it; runtime stop and outer shutdown close dispose it.")]
    async Task InstallManifestAsync(DatasetSubscriptionManifest manifest)
    {
        Log.Information("{Component}.{Method} installing manifest {ManifestRevision} for {Dataset}",
            "DatasetWorker", "InstallManifestAsync", manifest.Revision, manifest.Dataset);
        await (StopRuntimeAsync()).ConfigureAwait(false);
        pipePublisher = new PipeDatasetWorkerPublisher(publications, worker.Dataset,
            worker.ValueDate, worker.WorkerInstanceId, manifest.Revision, retentionAcknowledgments);
        try
        {
            datasetRuntime = await (DatasetWorkerRuntime.StartAsync(manifest, worker.DeploymentProfile,
                worker.DataSource, worker.Synthetic, pipePublisher, stopping.Token, worker.OptionPricingRefresh, loggerFactory)).ConfigureAwait(false);
        }
        catch
        {
            await pipePublisher.DisposeAsync().ConfigureAwait(false);
            pipePublisher = null;
            throw;
        }
        if (!datasetRuntime.IsHealthy || datasetRuntime.GenerationId == Guid.Empty)
            throw new InvalidOperationException("The replacement dataset generation is not healthy.");
        generation = datasetRuntime.GenerationId;
        await (pipePublisher.BindGenerationAsync(generation, stopping.Token)).ConfigureAwait(false);
        currentManifest = manifest;
        Log.Information("{Component}.{Method} runtime ready with {GenerationId} and {ManifestRevision}",
            "DatasetWorker", "InstallManifestAsync", generation, manifest.Revision);
    }

    async Task StopRuntimeAsync()
    {
        if (datasetRuntime is not null)
        {
            await (datasetRuntime.DisposeAsync()).ConfigureAwait(false);
            datasetRuntime = null;
        }
        if (pipePublisher is not null)
        {
            // Producers may flush durable option evidence while stopping. Keep the
            // publication/receipt path alive until producer shutdown has finished.
            await pipePublisher.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            await (pipePublisher.DisposeAsync()).ConfigureAwait(false);
            pipePublisher = null;
        }
    }

    async ValueTask WriteAsync(
        DatasetWorkerMessageKind kind,
        bool healthy,
        string detail,
        Guid correlationId,
        WorkerOptionChainResult? chainResult = null,
        CompositionSnapshotResult? compositionResult = null, byte[]? apiResult = null, string? apiError = null)
    {
        // A failed reconstruction may own an unaccepted epoch while the control identity
        // still names its predecessor. Do not attach cross-generation diagnostics.
        var diagnostics = datasetRuntime is { } activeRuntime && activeRuntime.GenerationId == generation
            ? activeRuntime.GetDiagnostics() : null;
        await (DatasetWorkerFrameCodec.WriteAsync(output, new()
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
            OptionChainResult = chainResult,
            CompositionResult = compositionResult,
            ApiResult = apiResult,
            ApiError = apiError,
            BootstrapToken = bootstrapToken
        }, 4 * 1024 * 1024, stopping.Token)).ConfigureAwait(false);
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


}


    }
}
