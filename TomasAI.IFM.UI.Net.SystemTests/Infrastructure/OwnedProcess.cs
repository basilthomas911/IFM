using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.UI.Net.SystemTests.Infrastructure;

public sealed class OwnedProcess : IAsyncDisposable
{
    readonly StreamWriter _standardOutput;
    readonly StreamWriter _standardError;
    readonly SecretRedactor _redactor;
    readonly object _outputGate = new();
    readonly StringBuilder _standardOutputSnapshot = new();
    readonly StringBuilder _standardErrorSnapshot = new();
    bool _disposed;

    OwnedProcess(Process process, StreamWriter standardOutput, StreamWriter standardError, SecretRedactor redactor)
    {
        Process = process;
        _standardOutput = standardOutput;
        _standardError = standardError;
        _redactor = redactor;
    }

    public Process Process { get; }
    public bool ForcedTermination { get; private set; }
    public string StandardOutputSnapshot
    {
        get
        {
            lock (_outputGate)
                return _standardOutputSnapshot.ToString();
        }
    }
    public string StandardErrorSnapshot
    {
        get
        {
            lock (_outputGate)
                return _standardErrorSnapshot.ToString();
        }
    }

    public static OwnedProcess Start(
        string executable,
        string logDirectory,
        SecretRedactor redactor,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        Directory.CreateDirectory(logDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = false
        };
        if (environment is not null)
        {
            foreach (var pair in environment)
                startInfo.Environment[pair.Key] = pair.Value;
        }

        var standardOutput = new StreamWriter(Path.Combine(logDirectory, "stdout.log"), append: false) { AutoFlush = true };
        var standardError = new StreamWriter(Path.Combine(logDirectory, "stderr.log"), append: false) { AutoFlush = true };
        try
        {
            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var owned = new OwnedProcess(process, standardOutput, standardError, redactor);
            process.OutputDataReceived += owned.OnOutputDataReceived;
            process.ErrorDataReceived += owned.OnErrorDataReceived;
            if (!process.Start())
                throw new InvalidOperationException($"Process did not start: {executable}");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return owned;
        }
        catch
        {
            standardOutput.Dispose();
            standardError.Dispose();
            throw;
        }
    }

    public async Task<bool> WaitForExitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Process.HasExited)
            return true;
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await Process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Process.HasExited;
        }
    }

    public async Task<bool> TerminateOwnedTreeAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Process.HasExited)
            return true;
        ForcedTermination = true;
        Process.Kill(entireProcessTree: true);
        return await WaitForExitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    public string Describe()
        => JsonSerializer.Serialize(new
        {
            Process.Id,
            Process.StartInfo.FileName,
            Process.StartInfo.WorkingDirectory,
            HasExited = SafeHasExited(),
            ForcedTermination
        }, new JsonSerializerOptions { WriteIndented = true });

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;
        Process.OutputDataReceived -= OnOutputDataReceived;
        Process.ErrorDataReceived -= OnErrorDataReceived;
        if (!Process.HasExited)
            await TerminateOwnedTreeAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
        Process.Dispose();
        lock (_outputGate)
        {
            _standardOutput.Dispose();
            _standardError.Dispose();
        }
    }

    void OnOutputDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is null)
            return;
        lock (_outputGate)
        {
            var line = _redactor.Redact(eventArgs.Data);
            _standardOutput.WriteLine(line);
            _standardOutputSnapshot.AppendLine(line);
        }
    }

    void OnErrorDataReceived(object sender, DataReceivedEventArgs eventArgs)
    {
        if (eventArgs.Data is null)
            return;
        lock (_outputGate)
        {
            var line = _redactor.Redact(eventArgs.Data);
            _standardError.WriteLine(line);
            _standardErrorSnapshot.AppendLine(line);
        }
    }

    bool SafeHasExited()
    {
        try { return Process.HasExited; }
        catch (InvalidOperationException) { return true; }
    }
}
