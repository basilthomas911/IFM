using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.ServerManager;

public sealed class ManagedProcessSupervisor : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly object _notificationSync = new();
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly IReadOnlyList<ManagedProcessDefinition> _definitions;
    private readonly TimeSpan _shutdownTimeout;
    private readonly Action<ManagedProcessLogEntry> _writeLog;
    private readonly Action<IReadOnlyCollection<ManagedProcessIdentity>>? _runningProcessesChanged;
    private readonly WindowsKillOnCloseJob? _developmentJob;
    private readonly Dictionary<string, OwnedProcess> _running = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> _lastCompletions = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public ManagedProcessSupervisor(
        IEnumerable<ManagedProcessDefinition> definitions,
        TimeSpan shutdownTimeout,
        Action<ManagedProcessLogEntry> writeLog,
        bool useDevelopmentKillOnCloseJob = false,
        Action<IReadOnlyCollection<ManagedProcessIdentity>>? runningProcessesChanged = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(writeLog);
        if (shutdownTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(shutdownTimeout));
        }

        _definitions = definitions.Where(definition => definition.Enabled).OrderBy(definition => definition.StartOrder).ToArray();
        _shutdownTimeout = shutdownTimeout;
        _writeLog = writeLog;
        _runningProcessesChanged = runningProcessesChanged;
        _developmentJob = useDevelopmentKillOnCloseJob ? new WindowsKillOnCloseJob() : null;
    }

    public IReadOnlyCollection<string> RunningProcessKeys
    {
        get
        {
            lock (_sync)
            {
                return _running.Keys.ToArray();
            }
        }
    }

    public IReadOnlyCollection<ManagedProcessIdentity> RunningProcesses
    {
        get
        {
            lock (_sync)
            {
                return _running.Values
                    .Select(value => value.Identity)
                    .ToArray();
            }
        }
    }

    public async Task StartAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            try
            {
                foreach (var definition in _definitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await StartCoreAsync(definition).ConfigureAwait(false);
                    await WaitForReadinessAsync(definition, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                await StopAllCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task RestartAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopAllCoreAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                foreach (var definition in _definitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await StartCoreAsync(definition).ConfigureAwait(false);
                    await WaitForReadinessAsync(definition, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                await StopAllCoreAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopAllAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopAllCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task WaitForExitAsync(string processKey, CancellationToken cancellationToken = default)
    {
        Task completion;
        lock (_sync)
        {
            if (!_lastCompletions.TryGetValue(processKey, out completion!))
            {
                throw new InvalidOperationException($"Process '{processKey}' has not been started.");
            }
        }

        await completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await StopAllAsync().ConfigureAwait(false);
        }
        finally
        {
            _developmentJob?.Dispose();
            _disposed = true;
            _lifecycle.Dispose();
        }
    }

    private Task StartCoreAsync(ManagedProcessDefinition definition)
    {
        lock (_sync)
        {
            if (_running.ContainsKey(definition.Key))
            {
                WriteLifecycle(definition, "Start skipped because the process is already running.");
                return Task.CompletedTask;
            }
        }

        var process = new Process
        {
            StartInfo = CreateStartInfo(definition),
            EnableRaisingEvents = true
        };

        try
        {
            var workingDirectory = definition.ResolveWorkingDirectory();
            if (!Directory.Exists(workingDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Working directory '{workingDirectory}' does not exist.");
            }

            var executablePath = definition.ResolveExecutablePath();
            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException("Managed executable was not found.", executablePath);
            }

            if (!process.Start())
            {
                throw new InvalidOperationException("Process.Start returned false.");
            }

            _developmentJob?.Assign(process);

            var owned = new OwnedProcess(
                definition,
                process,
                new ManagedProcessIdentity(
                    definition.Key,
                    process.Id,
                    new DateTimeOffset(process.StartTime.ToUniversalTime()),
                    definition.ResolveExecutablePath(),
                    definition.StartOrder));
            lock (_sync)
            {
                _running.Add(definition.Key, owned);
                owned.Completion = MonitorAsync(owned);
                _lastCompletions[definition.Key] = owned.Completion;
            }
        }
        catch (Exception exception)
        {
            try
            {
                if (process.Id != 0 && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }
            catch (InvalidOperationException)
            {
                // The process did not start or exited during startup cleanup.
            }

            process.Dispose();
            WriteLifecycle(definition, $"Start failed: {exception.Message}");
            lock (_sync)
            {
                _lastCompletions[definition.Key] = Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        NotifyRunningProcessesChanged();

        WriteLifecycle(definition, $"Started process {process.Id}.");

        return Task.CompletedTask;
    }

    private ProcessStartInfo CreateStartInfo(ManagedProcessDefinition definition)
    {
        var startInfo = new ProcessStartInfo
        {
            CreateNoWindow = definition.WindowStyle == ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            WindowStyle = definition.WindowStyle,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = definition.ShutdownMode == ProcessShutdownMode.StandardInput,
            FileName = definition.ResolveExecutablePath(),
            WorkingDirectory = definition.ResolveWorkingDirectory()
        };

        foreach (var argument in definition.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in definition.EnvironmentVariables)
        {
            startInfo.Environment[variable.Key] = variable.Value;
        }

        return startInfo;
    }

    private async Task MonitorAsync(OwnedProcess owned)
    {
        var stdout = PumpAsync(owned, owned.Process.StandardOutput, ManagedProcessLogStream.StandardOutput);
        var stderr = PumpAsync(owned, owned.Process.StandardError, ManagedProcessLogStream.StandardError);

        try
        {
            await owned.Process.WaitForExitAsync().ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            WriteLifecycle(owned.Definition, $"Exited with code {owned.Process.ExitCode}.");
        }
        catch (Exception exception)
        {
            WriteLifecycle(owned.Definition, $"Process monitoring failed: {exception.Message}");
        }
        finally
        {
            lock (_sync)
            {
                if (_running.TryGetValue(owned.Definition.Key, out var current) && ReferenceEquals(current, owned))
                {
                    _running.Remove(owned.Definition.Key);
                }
            }

            NotifyRunningProcessesChanged();

            owned.Process.Dispose();
        }
    }

    private async Task PumpAsync(OwnedProcess owned, StreamReader reader, ManagedProcessLogStream stream)
    {
        try
        {
            while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                Write(owned.Definition, stream, line);
            }
        }
        catch (Exception exception)
        {
            WriteLifecycle(owned.Definition, $"{stream} capture failed: {exception.Message}");
        }
    }

    private async Task WaitForReadinessAsync(
        ManagedProcessDefinition definition,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(definition.ReadinessUri))
        {
            return;
        }

        WriteLifecycle(definition, $"Waiting for readiness at {definition.ReadinessUri}.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(definition.ReadinessTimeoutSeconds));
        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        HttpStatusCode? lastStatus = null;
        Exception? lastException = null;
        try
        {
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (!IsRunning(definition.Key))
                {
                    throw new InvalidOperationException(
                        $"Process '{definition.Key}' exited before its readiness endpoint became healthy.");
                }

                try
                {
                    using var response = await client.GetAsync(definition.ReadinessUri, timeout.Token)
                        .ConfigureAwait(false);
                    lastStatus = response.StatusCode;
                    lastException = null;
                    if (response.IsSuccessStatusCode)
                    {
                        WriteLifecycle(definition, $"Readiness confirmed with HTTP {(int)response.StatusCode}.");
                        return;
                    }
                }
                catch (HttpRequestException exception)
                {
                    lastException = exception;
                }

                await Task.Delay(definition.ReadinessPollIntervalMilliseconds, timeout.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var detail = lastStatus is not null
                ? $"last HTTP status was {(int)lastStatus.Value}"
                : lastException is not null
                    ? lastException.Message
                    : "no response was received";
            var message = $"Readiness timed out after {definition.ReadinessTimeoutSeconds} seconds: {detail}.";
            WriteLifecycle(definition, message);
            throw new TimeoutException(message);
        }
    }

    private bool IsRunning(string processKey)
    {
        lock (_sync)
        {
            return _running.ContainsKey(processKey);
        }
    }

    private async Task StopAllCoreAsync(CancellationToken cancellationToken)
    {
        OwnedProcess[] processes;
        lock (_sync)
        {
            processes = _running.Values.OrderByDescending(value => value.Definition.StartOrder).ToArray();
        }

        foreach (var owned in processes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await StopCoreAsync(owned, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task StopCoreAsync(OwnedProcess owned, CancellationToken cancellationToken)
    {
        if (HasExited(owned.Process))
        {
            await owned.Completion.ConfigureAwait(false);
            return;
        }

        var gracefulRequested = TryRequestGracefulShutdown(owned);
        if (gracefulRequested)
        {
            WriteLifecycle(owned.Definition, "Graceful shutdown requested.");
            try
            {
                await owned.Process.WaitForExitAsync(cancellationToken)
                    .WaitAsync(_shutdownTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                WriteLifecycle(
                    owned.Definition,
                    $"Graceful shutdown exceeded {_shutdownTimeout.TotalSeconds:0.###} seconds; forcing process-tree termination.");
            }
        }
        else
        {
            WriteLifecycle(owned.Definition, "No graceful shutdown channel was available; forcing process-tree termination.");
        }

        if (!HasExited(owned.Process))
        {
            try
            {
                owned.Process.Kill(entireProcessTree: true);
                WriteLifecycle(owned.Definition, "Forced process-tree termination requested.");
            }
            catch (InvalidOperationException)
            {
                // The process exited between the state check and Kill.
            }
        }

        await owned.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool HasExited(Process process)
    {
        try
        {
            return process.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private bool TryRequestGracefulShutdown(OwnedProcess owned)
    {
        try
        {
            return owned.Definition.ShutdownMode switch
            {
                ProcessShutdownMode.CloseMainWindow => owned.Process.CloseMainWindow(),
                ProcessShutdownMode.StandardInput => WriteShutdownInput(owned),
                _ => false
            };
        }
        catch (Exception exception)
        {
            WriteLifecycle(owned.Definition, $"Graceful shutdown request failed: {exception.Message}");
            return false;
        }
    }

    private static bool WriteShutdownInput(OwnedProcess owned)
    {
        owned.Process.StandardInput.WriteLine(owned.Definition.ShutdownInput);
        owned.Process.StandardInput.Flush();
        return true;
    }

    private void WriteLifecycle(ManagedProcessDefinition definition, string message)
        => Write(definition, ManagedProcessLogStream.Lifecycle, message);

    private void Write(ManagedProcessDefinition definition, ManagedProcessLogStream stream, string message)
        => _writeLog(new ManagedProcessLogEntry(
            DateTimeOffset.Now,
            definition.Key,
            definition.DisplayName,
            stream,
            message));

    private void NotifyRunningProcessesChanged()
    {
        if (_runningProcessesChanged is null)
        {
            return;
        }

        lock (_notificationSync)
        {
            try
            {
                _runningProcessesChanged(RunningProcesses);
            }
            catch (Exception exception)
            {
                _writeLog(new ManagedProcessLogEntry(
                    DateTimeOffset.Now,
                    "manager",
                    "Server Manager",
                    ManagedProcessLogStream.Manager,
                    $"Development process-session record update failed: {exception.Message}"));
            }
        }
    }

    private sealed class OwnedProcess(
        ManagedProcessDefinition definition,
        Process process,
        ManagedProcessIdentity identity)
    {
        public ManagedProcessDefinition Definition { get; } = definition;

        public Process Process { get; } = process;

        public ManagedProcessIdentity Identity { get; } = identity;

        public Task Completion { get; set; } = Task.CompletedTask;
    }
}

public sealed record ManagedProcessIdentity(
    string ProcessKey,
    int ProcessId,
    DateTimeOffset StartedAtUtc,
    string ExecutablePath,
    int StartOrder);
