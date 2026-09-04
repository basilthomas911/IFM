using System.Diagnostics;
using FluentAssertions;
using TomasAI.IFM.Application.ServerManager.TestProcess;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

public sealed class DevelopmentProcessSessionTests
{
    [Fact]
    public void Rejects_a_second_active_development_manager_session()
    {
        var sessionFile = NewSessionFile();
        var singletonName = $"Local\\IFM.DevelopmentProcessSessionTests.{Guid.NewGuid():N}";
        try
        {
            using var first = new DevelopmentProcessSession(sessionFile, singletonName);

            var action = () => new DevelopmentProcessSession(sessionFile, singletonName);

            action.Should().Throw<InvalidOperationException>()
                .WithMessage("*already active*");
        }
        finally
        {
            DeleteSessionDirectory(sessionFile);
        }
    }

    [Fact]
    public async Task Reconciles_only_a_process_with_matching_pid_start_time_and_executable()
    {
        var sessionFile = NewSessionFile();
        var mutexName = $"Local\\IFM.DevelopmentProcessSessionTests.{Guid.NewGuid():N}";
        using var process = StartLongRunningHelper();
        try
        {
            using (var first = new DevelopmentProcessSession(sessionFile, mutexName))
            {
                first.Record(
                [
                    new ManagedProcessIdentity(
                        "api",
                        process.Id,
                        new DateTimeOffset(process.StartTime.ToUniversalTime()),
                        Path.GetFullPath(process.StartInfo.FileName),
                        10)
                ]);
            }

            using var replacement = new DevelopmentProcessSession(sessionFile, mutexName);
            var logs = new List<string>();
            await replacement.ReconcilePreviousSessionAsync(logs.Add);

            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            process.HasExited.Should().BeTrue();
            File.Exists(sessionFile).Should().BeFalse();
            logs.Should().Contain(message => message.Contains("PID"));
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            DeleteSessionDirectory(sessionFile);
        }
    }

    [Fact]
    public async Task Identity_mismatch_is_reported_without_terminating_process()
    {
        var sessionFile = NewSessionFile();
        var mutexName = $"Local\\IFM.DevelopmentProcessSessionTests.{Guid.NewGuid():N}";
        using var process = StartLongRunningHelper();
        try
        {
            using (var first = new DevelopmentProcessSession(sessionFile, mutexName))
            {
                first.Record(
                [
                    new ManagedProcessIdentity(
                        "api",
                        process.Id,
                        new DateTimeOffset(process.StartTime.ToUniversalTime()),
                        Path.Combine(Path.GetTempPath(), $"not-the-process-{Guid.NewGuid():N}.exe"),
                        10)
                ]);
            }

            using var replacement = new DevelopmentProcessSession(sessionFile, mutexName);
            var action = () => replacement.ReconcilePreviousSessionAsync(_ => { });

            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*No process was terminated*");
            process.HasExited.Should().BeFalse();
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            DeleteSessionDirectory(sessionFile);
        }
    }

    private static Process StartLongRunningHelper()
    {
        var dotnet = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");
        var helperAssembly = typeof(TestProcessMarker).Assembly.Location;
        var startInfo = new ProcessStartInfo
        {
            FileName = dotnet,
            WorkingDirectory = Path.GetDirectoryName(helperAssembly)!,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(helperAssembly);
        startInfo.ArgumentList.Add("--delay-ms");
        startInfo.ArgumentList.Add("30000");
        return Process.Start(startInfo)!;
    }

    private static string NewSessionFile()
        => Path.Combine(Path.GetTempPath(), $"ifm-session-{Guid.NewGuid():N}", "session.json");

    private static void DeleteSessionDirectory(string sessionFile)
    {
        var directory = Path.GetDirectoryName(sessionFile)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
