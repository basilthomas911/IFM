using TomasAI.IFM.Application.Api.Server.Core.Recovery.Databento.Contracts;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace TomasAI.IFM.Domain.MarketData.Feed.IntegrationTests.Recovery;

/// <summary>Verifies terminal recovery in a disposable API process, never the test runner or trading API.</summary>
public sealed class HardResetTerminalHostVerificationTests(ITestOutputHelper output)
{
    static readonly string[] RecoverySequence =
    [
        nameof(IApiDatabentoRecoveryActions.CaptureRecoveryInputsAsync),
        nameof(IApiDatabentoRecoveryActions.FenceFailedGenerationAsync),
        nameof(IApiDatabentoRecoveryActions.StopDatabentoWorkersAsync),
        nameof(IApiDatabentoRecoveryActions.StartDatabentoWorkersAsync),
        nameof(IApiDatabentoRecoveryActions.QualifyDatabentoAsync),
        nameof(IApiDatabentoRecoveryActions.StartPublisherAsync),
        nameof(IApiDatabentoRecoveryActions.AdmitGenerationAsync)
    ];

    /// <summary>Provides one independently reported exception scenario per recovery action.</summary>
    public static IEnumerable<object[]> ActionFailures() => RecoverySequence.Select(action => new object[] { action });

    /// <summary>
    /// Injects an exception at one action boundary in a real child API host. Earlier actions,
    /// infrastructure, actor reconciliation, notification, and shutdown are production code.
    /// This minimum matrix does not claim all internal dependency failure modes are exercised.
    /// </summary>
    [Theory]
    [MemberData(nameof(ActionFailures))]
    public async Task Each_recovery_action_exception_is_logged_and_terminates_real_API(string failedAction)
    {
        var text = await RunChildAsync("host-action-failure", failedAction);
        const string prefix = "ACTION_FAILURE_EVIDENCE=";
        var evidenceLine = Assert.Single(text.Split('\n'), line => line.StartsWith(prefix, StringComparison.Ordinal));
        using var document = JsonDocument.Parse(evidenceLine[prefix.Length..]);
        var evidence = document.RootElement;
        var index = Array.IndexOf(RecoverySequence, failedAction);
        var correlation = evidence.GetProperty("CorrelationId").GetGuid();
        Assert.NotEqual(Guid.Empty, correlation);
        Assert.Equal(failedAction, evidence.GetProperty("Action").GetString());
        Assert.Equal(failedAction, evidence.GetProperty("FailedStage").GetString());
        Assert.Equal("Unrecoverable", evidence.GetProperty("Outcome").GetString());
        Assert.Equal("RecoveryActionInjectedException", evidence.GetProperty("ExceptionType").GetString());
        var exceptionMessage = $"Injected recovery action failure: {failedAction}; correlation={correlation:D}";
        Assert.Equal(exceptionMessage, evidence.GetProperty("ExceptionMessage").GetString());
        Assert.Contains(exceptionMessage, evidence.GetProperty("Detail").GetString());
        Assert.Contains("RecoveryActionInjectedException", evidence.GetProperty("Detail").GetString());
        Assert.Contains("RecoveryActionFaultProbe.ExecuteAsync", evidence.GetProperty("Detail").GetString());
        Assert.True(evidence.GetProperty("TerminalLatched").GetBoolean());
        Assert.False(evidence.GetProperty("CandidateAdmittedAtFailure").GetBoolean());
        Assert.Equal(index >= 4 ? 1 : 0, evidence.GetProperty("CandidateCountAtFailure").GetInt32());

        string[] failureActions = [nameof(IApiDatabentoRecoveryActions.NotifySystemConsoleAsync),
            nameof(IApiDatabentoRecoveryActions.ShutdownApiAsync)];
        var expectedPrefix = RecoverySequence.Take(index + 1).ToArray();
        if (failedAction is nameof(IApiDatabentoRecoveryActions.StartDatabentoWorkersAsync)
            or nameof(IApiDatabentoRecoveryActions.QualifyDatabentoAsync))
        {
            var retry = RecoverySequence.Skip(2).Take(index - 1);
            expectedPrefix = expectedPrefix.Concat(retry).Concat(retry).ToArray();
        }
        var expectedCalls = expectedPrefix.Concat(failureActions).ToArray();
        Assert.Equal(expectedCalls, evidence.GetProperty("Calls").EnumerateArray().Select(item => item.GetString()).ToArray());
        var logs = evidence.GetProperty("Logs").EnumerateArray().ToArray();
        Assert.All(logs, entry => Assert.Equal(correlation, entry.GetProperty("CorrelationId").GetGuid()));
        Assert.Equal(expectedCalls, logs.Where(entry => entry.GetProperty("EventId").GetInt32() == 17400)
            .Select(entry => entry.GetProperty("Action").GetString()).ToArray());
        Assert.Equal(expectedPrefix.Where(action => action != failedAction).Concat(failureActions).ToArray(),
            logs.Where(entry => entry.GetProperty("EventId").GetInt32() == 17401)
                .Select(entry => entry.GetProperty("Action").GetString()).ToArray());
        var failure = Assert.Single(logs, entry => entry.GetProperty("EventId").GetInt32() == 17402);
        Assert.Equal(failedAction, failure.GetProperty("Action").GetString());
        Assert.Contains("RecoveryActionInjectedException", failure.GetProperty("Exception").GetString());
        Assert.Contains(exceptionMessage, failure.GetProperty("Exception").GetString());
        Assert.DoesNotContain(logs, entry => entry.GetProperty("EventId").GetInt32() == 17403);
        output.WriteLine("Verified exact action exception, stopped sequence, terminal latch and real shutdown: " + failedAction);
    }

    [Theory]
    [InlineData("host-terminal")]
    [InlineData("host-console-throw")]
    [InlineData("host-console-hang")]
    public async Task Failed_action_notifies_then_stops_real_API_with_nonzero_exit_and_no_owned_workers(string mode) =>
        _ = await RunChildAsync(mode);

    async Task<string> RunChildAsync(string mode, string? failedAction = null)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TomasAI.IFM.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Path.Combine(root.FullName, "TomasAI.IFM.Application.Api.Server.RecoveryChildProbe",
            "bin", configuration, "net10.0", "TomasAI.IFM.Application.Api.Server.RecoveryChildProbe.dll");
        Assert.True(File.Exists(assembly), "Build the child verification project before running this test.");
        using var child = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = root.FullName, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                ArgumentList = { assembly, mode }
            }
        };
        if (failedAction is not null) child.StartInfo.ArgumentList.Add(failedAction);
        var evidenceName = failedAction is null ? mode : mode + "-" + failedAction;
        Assert.True(child.Start());
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try { await child.WaitForExitAsync(deadline.Token); }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync();
            }
            var artifacts = Environment.GetEnvironmentVariable("IFM_QUALIFICATION_ARTIFACT_DIRECTORY");
            if (!string.IsNullOrWhiteSpace(artifacts))
            {
                Directory.CreateDirectory(artifacts);
                await File.WriteAllTextAsync(Path.Combine(artifacts, evidenceName + ".stdout.log"), await stdout);
                await File.WriteAllTextAsync(Path.Combine(artifacts, evidenceName + ".stderr.log"), await stderr);
                output.WriteLine("Evidence directory: " + artifacts);
            }
        }
        var text = await stdout;
        output.WriteLine("Mode={0}; exitCode={1}; stderr={2}", evidenceName, child.ExitCode, await stderr);
        Assert.True(child.ExitCode == 42, $"Expected terminal exit 42, got {child.ExitCode}.\n{text}\n{await stderr}");
        Assert.Contains("REAL_KESTREL_ACTOR_HOST_READY=", text);
        Assert.Contains("TERMINAL_ACTION=" + (failedAction ?? nameof(IApiDatabentoRecoveryActions.StartPublisherAsync)), text);
        Assert.Contains("SYSTEM_CONSOLE_ATTEMPT=", text);
        Assert.Contains("REAL_OTEL_LOCAL_SUBMISSION=", text);
        Assert.Contains("REAL_API_HOST_DISPOSED", text);
        Assert.True(text.IndexOf("SYSTEM_CONSOLE_ATTEMPT=", StringComparison.Ordinal)
            < text.IndexOf("Unrecoverable Databento recovery;", StringComparison.Ordinal));
        if (mode is "host-terminal" or "host-action-failure") Assert.Contains("SYSTEM_CONSOLE_SENT", text);
        else Assert.Contains("Hard reset failure notification did not complete", text);

        var pids = Regex.Matches(text, @"OWNED_WORKER_PID=(\d+)")
            .Select(match => int.Parse(match.Groups[1].Value)).Distinct().ToArray();
        var expectedWorkerCount = failedAction is nameof(IApiDatabentoRecoveryActions.QualifyDatabentoAsync) ? 4
            : failedAction is null || Array.IndexOf(RecoverySequence, failedAction) >= 4 ? 2 : 1;
        Assert.Equal(expectedWorkerCount, pids.Length);
        foreach (var pid in pids)
        {
            try
            {
                using var worker = Process.GetProcessById(pid);
                await worker.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                Assert.True(worker.HasExited);
            }
            catch (ArgumentException) { /* Already exited. */ }
        }
        output.WriteLine("Verified worker exit: " + string.Join(", ", pids));
        return text;
    }
}
