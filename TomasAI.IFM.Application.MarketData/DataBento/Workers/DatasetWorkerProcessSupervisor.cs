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
}

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
}

/// <summary>Owns one exact child process and its duplex local control channel.</summary>
public sealed class DatasetWorkerProcessSupervisor : IAsyncDisposable
{
    readonly DatabentoStage3Options options;
    readonly Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? publicationIngress;
    readonly SemaphoreSlim commands = new(1, 1);
    AnonymousPipeServerStream? commandPipe;
    AnonymousPipeServerStream? responsePipe;
    AnonymousPipeServerStream? publicationPipe;
    CancellationTokenSource? publicationStopping;
    Task? publicationReader;
    Process? process;
    WindowsJob? windowsJob;
    DatasetWorkerStartRequest? identity;
    DateTime startedOnUtc;
    long sequence;
    long responseSequence;
    string bootstrapToken = string.Empty;
    bool graceful;
    bool forced;
    bool responsive;
    bool dataPlaneHealthy = true;
    DatasetWorkerControlFrame? lastFrame;

    public DatasetWorkerProcessSupervisor(
        DatabentoStage3Options options,
        Func<DatasetPublicationEnvelope, CancellationToken, ValueTask>? publicationIngress = null)
    {
        this.options = options.Validate();
        this.publicationIngress = publicationIngress;
    }

    public event Action<DatasetWorkerProcessSnapshot>? Exited;

    public DatasetWorkerProcessSnapshot Current => Snapshot();

    public async Task<DatasetWorkerProcessSnapshot> StartAsync(
        DatasetWorkerStartRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (process is { HasExited: false })
            throw new InvalidOperationException("A dataset worker process is already running.");
        var executable = Path.GetFullPath(request.ExecutablePath);
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            throw new FileNotFoundException("The configured dataset worker executable does not exist.", executable);
        if (request.WorkerInstanceId == Guid.Empty || request.GenerationId == Guid.Empty
            || request.ValueDate == default || string.IsNullOrWhiteSpace(request.Dataset)
            || request.ManifestRevision < 1)
            throw new ArgumentException("Dataset worker start identity is invalid.", nameof(request));

        sequence = 0;
        responseSequence = 0;
        graceful = false;
        forced = false;
        responsive = false;
        dataPlaneHealthy = true;
        lastFrame = null;

        commandPipe = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.Inheritable);
        responsePipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        publicationPipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        bootstrapToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        start.Environment["IFM_DATASET_WORKER_BOOTSTRAP"] = bootstrapToken;
        foreach (var value in request.PrefixArguments) start.ArgumentList.Add(value);
        start.ArgumentList.Add("--control-in");
        start.ArgumentList.Add(commandPipe.GetClientHandleAsString());
        start.ArgumentList.Add("--control-out");
        start.ArgumentList.Add(responsePipe.GetClientHandleAsString());
        start.ArgumentList.Add("--publication-out");
        start.ArgumentList.Add(publicationPipe.GetClientHandleAsString());
        start.ArgumentList.Add("--dataset");
        start.ArgumentList.Add(request.Dataset);
        start.ArgumentList.Add("--value-date");
        start.ArgumentList.Add(request.ValueDate.ToString("yyyy-MM-dd"));
        start.ArgumentList.Add("--worker-id");
        start.ArgumentList.Add(request.WorkerInstanceId.ToString("D"));
        start.ArgumentList.Add("--generation-id");
        start.ArgumentList.Add(request.GenerationId.ToString("D"));

        identity = request with { ExecutablePath = executable };
        process = Process.Start(start) ?? throw new InvalidOperationException("Dataset worker process did not start.");
        startedOnUtc = process.StartTime.ToUniversalTime();
        if (OperatingSystem.IsWindows())
        {
            windowsJob = new WindowsJob();
            windowsJob.Assign(process);
        }
        commandPipe.DisposeLocalCopyOfClientHandle();
        responsePipe.DisposeLocalCopyOfClientHandle();
        publicationPipe.DisposeLocalCopyOfClientHandle();
        publicationStopping = new CancellationTokenSource();
        publicationReader = ReadPublicationsAsync(publicationStopping.Token);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => Exited?.Invoke(Snapshot());

        using var handshake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        handshake.CancelAfter(options.WorkerHandshakeTimeout);
        var hello = await DatasetWorkerFrameCodec.ReadAsync(responsePipe,
            options.ControlFrameMaximumBytes, handshake.Token).ConfigureAwait(false);
        ValidateResponse(hello, DatasetWorkerMessageKind.WorkerHello);
        lastFrame = hello;
        await SendAsync(DatasetWorkerMessageKind.SupervisorHello,
            "Supervisor accepted worker identity.", cancellationToken).ConfigureAwait(false);
        var ready = await DatasetWorkerFrameCodec.ReadAsync(responsePipe,
            options.ControlFrameMaximumBytes, handshake.Token).ConfigureAwait(false);
        ValidateResponse(ready, DatasetWorkerMessageKind.WorkerReady, allowGenerationChange: true);
        identity = identity with { GenerationId = ready.GenerationId };
        lastFrame = ready;
        responsive = true;
        return Snapshot();
    }

    public async Task<DatasetWorkerControlFrame> GetHealthAsync(CancellationToken cancellationToken = default)
        => await SendAndReceiveAsync(DatasetWorkerMessageKind.HealthSnapshot,
            DatasetWorkerMessageKind.HealthSnapshot, cancellationToken).ConfigureAwait(false);

    public async Task<DatasetWorkerControlFrame> ResetAsync(CancellationToken cancellationToken = default)
        => await SendAndReceiveAsync(DatasetWorkerMessageKind.CooperativeReset,
            DatasetWorkerMessageKind.ResetCompleted, cancellationToken,
            allowGenerationChange: true).ConfigureAwait(false);

    public async Task<DatasetWorkerProcessSnapshot> StopAsync(CancellationToken cancellationToken = default)
    {
        if (process is null) return Snapshot();
        if (!process.HasExited)
        {
            try
            {
                await SendAndReceiveAsync(DatasetWorkerMessageKind.GracefulStop,
                    DatasetWorkerMessageKind.Stopped, cancellationToken,
                    options.WorkerGracefulStopTimeout).ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken)
                    .WaitAsync(options.WorkerGracefulStopTimeout, cancellationToken).ConfigureAwait(false);
                graceful = true;
            }
            catch (Exception exception) when (exception is not OperationCanceledException
                                               || !cancellationToken.IsCancellationRequested)
            {
                if (!process.HasExited)
                {
                    forced = true;
                    await ForceTerminateAsync(process, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        return Snapshot();
    }

    public async Task HangAsync(CancellationToken cancellationToken = default)
        => await SendAsync(DatasetWorkerMessageKind.Hang, "Injected non-cooperative worker.", cancellationToken)
            .ConfigureAwait(false);

    async Task ForceTerminateAsync(Process child, CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsLinux())
        {
            // The worker calls setpgid(0, 0) before opening its control loop. Targeting the
            // negative exact child PID terminates that process group, including descendants.
            _ = UnixNative.kill(-child.Id, UnixNative.SigTerm);
            try
            {
                await child.WaitForExitAsync(cancellationToken)
                    .WaitAsync(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (TimeoutException) { }
            _ = UnixNative.kill(-child.Id, UnixNative.SigKill);
        }
        else
        {
            child.Kill(entireProcessTree: true);
        }
        await child.WaitForExitAsync(cancellationToken)
            .WaitAsync(options.WorkerForceKillTimeout, cancellationToken).ConfigureAwait(false);
    }

    async Task<DatasetWorkerControlFrame> SendAndReceiveAsync(
        DatasetWorkerMessageKind request,
        DatasetWorkerMessageKind expected,
        CancellationToken cancellationToken,
        TimeSpan? timeout = null,
        bool allowGenerationChange = false)
    {
        await commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SendCoreAsync(request, request.ToString(), cancellationToken).ConfigureAwait(false);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout ?? options.WorkerCommandTimeout);
            var response = await DatasetWorkerFrameCodec.ReadAsync(responsePipe!,
                options.ControlFrameMaximumBytes, deadline.Token).ConfigureAwait(false);
            ValidateResponse(response, expected, allowGenerationChange);
            if (allowGenerationChange)
                identity = identity! with { GenerationId = response.GenerationId };
            lastFrame = response;
            responsive = true;
            return response;
        }
        catch
        {
            responsive = false;
            throw;
        }
        finally { commands.Release(); }
    }

    async Task SendAsync(DatasetWorkerMessageKind kind, string detail, CancellationToken cancellationToken)
    {
        await commands.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await SendCoreAsync(kind, detail, cancellationToken).ConfigureAwait(false); }
        finally { commands.Release(); }
    }

    async Task ReadPublicationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var publication = await DatasetPublicationFrameCodec.ReadAsync(
                    publicationPipe!, cancellationToken).ConfigureAwait(false);
                if (publicationIngress is not null)
                    await publicationIngress(publication, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (EndOfStreamException) { }
        catch (IOException) when (process is null || process.HasExited) { }
        catch
        {
            dataPlaneHealthy = false;
        }
    }

    ValueTask SendCoreAsync(DatasetWorkerMessageKind kind, string detail, CancellationToken cancellationToken)
    {
        var current = identity ?? throw new InvalidOperationException("Dataset worker has not started.");
        return DatasetWorkerFrameCodec.WriteAsync(commandPipe!, new()
        {
            Kind = kind,
            WorkerInstanceId = current.WorkerInstanceId,
            Dataset = current.Dataset,
            ValueDate = current.ValueDate,
            GenerationId = current.GenerationId,
            CorrelationId = Guid.NewGuid(),
            Sequence = Interlocked.Increment(ref sequence),
            ProcessId = Environment.ProcessId,
            Detail = detail,
            BootstrapToken = bootstrapToken
        }, options.ControlFrameMaximumBytes, cancellationToken);
    }

    void ValidateResponse(
        DatasetWorkerControlFrame response,
        DatasetWorkerMessageKind expected,
        bool allowGenerationChange = false)
    {
        var current = identity!;
        if (response.Kind != expected
            || response.WorkerInstanceId != current.WorkerInstanceId
            || response.Dataset != current.Dataset
            || response.ValueDate != current.ValueDate
            || !allowGenerationChange && response.GenerationId != current.GenerationId
            || response.ProcessId != process!.Id
            || response.Sequence <= responseSequence
            || !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(response.BootstrapToken),
                Encoding.ASCII.GetBytes(bootstrapToken)))
            throw new InvalidDataException("Dataset worker response identity does not match the supervised process.");
        responseSequence = response.Sequence;
    }

    DatasetWorkerProcessSnapshot Snapshot()
    {
        var current = identity;
        var child = process;
        var running = child is not null && !child.HasExited;
        return new()
        {
            Dataset = current?.Dataset ?? string.Empty,
            WorkerInstanceId = current?.WorkerInstanceId ?? Guid.Empty,
            GenerationId = current?.GenerationId ?? Guid.Empty,
            ProcessId = child?.Id ?? 0,
            StartedOnUtc = startedOnUtc,
            Running = running,
            Healthy = running && responsive && dataPlaneHealthy && lastFrame?.Healthy == true,
            GracefulStopSucceeded = graceful,
            ForcedTermination = forced,
            ExitCode = child is { HasExited: true } ? child.ExitCode : null,
            Detail = lastFrame?.Detail ?? string.Empty
        };
    }

    public async ValueTask DisposeAsync()
    {
        try { await StopAsync().ConfigureAwait(false); }
        finally
        {
            publicationStopping?.Cancel();
            if (publicationReader is not null)
            {
                try { await publicationReader.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            commandPipe?.Dispose();
            responsePipe?.Dispose();
            publicationPipe?.Dispose();
            publicationStopping?.Dispose();
            process?.Dispose();
            windowsJob?.Dispose();
            commands.Dispose();
        }
    }

    sealed class WindowsJob : IDisposable
    {
        const uint KillOnClose = 0x00002000;
        readonly SafeJobHandle handle;

        public WindowsJob()
        {
            handle = Native.CreateJobObject(IntPtr.Zero, null);
            if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
            var limits = new ExtendedLimitInformation
            {
                BasicLimitInformation = new BasicLimitInformation { LimitFlags = KillOnClose }
            };
            var size = Marshal.SizeOf<ExtendedLimitInformation>();
            var pointer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(limits, pointer, false);
                if (!Native.SetInformationJobObject(handle, 9, pointer, (uint)size))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            finally { Marshal.FreeHGlobal(pointer); }
        }

        public void Assign(Process value)
        {
            if (!Native.AssignProcessToJobObject(handle, value.Handle))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        public void Dispose() => handle.Dispose();

        [StructLayout(LayoutKind.Sequential)] struct BasicLimitInformation
        {
            public long PerProcessUserTimeLimit, PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass, SchedulingClass;
        }
        [StructLayout(LayoutKind.Sequential)] struct IoCounters
        {
            public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount,
                ReadTransferCount, WriteTransferCount, OtherTransferCount;
        }
        [StructLayout(LayoutKind.Sequential)] struct ExtendedLimitInformation
        {
            public BasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
        }
        sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            SafeJobHandle() : base(true) { }
            protected override bool ReleaseHandle() => Native.CloseHandle(handle);
        }
        static class Native
        {
            [DllImport("kernel32.dll", EntryPoint = "CreateJobObjectW", SetLastError = true, CharSet = CharSet.Unicode)]
            internal static extern SafeJobHandle CreateJobObject(IntPtr attributes, string? name);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetInformationJobObject(SafeJobHandle job, int kind, IntPtr value, uint length);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr value);
        }
    }

    static class UnixNative
    {
        internal const int SigTerm = 15;
        internal const int SigKill = 9;
        [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
        internal static extern int kill(int processId, int signal);
    }
}
