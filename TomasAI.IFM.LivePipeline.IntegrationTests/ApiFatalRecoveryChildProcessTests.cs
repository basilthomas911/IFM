using System.Diagnostics;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class ApiFatalRecoveryChildProcessTests
{
    [Theory]
    [InlineData("graceful", false)]
    [InlineData("hung", true)]
    [InlineData("throw", true)]
    public async Task Fatal_shutdown_exits_child_process_nonzero_with_independent_evidence(
        string mode, bool expectStderr)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TomasAI.IFM.sln")))
            root = root.Parent;
        Assert.NotNull(root);
        var relativeOutput = Path.GetRelativePath(
            Path.Combine(root.FullName, "TomasAI.IFM.LivePipeline.IntegrationTests", "bin"), AppContext.BaseDirectory);
        var assembly = Path.Combine(root.FullName,
            "TomasAI.IFM.Application.Api.Server.RecoveryChildProbe",
            "bin", relativeOutput, "TomasAI.IFM.Application.Api.Server.RecoveryChildProbe.dll");
        Assert.True(File.Exists(assembly), "The referenced recovery child probe must be built before verification.");
        using var child = new Process { StartInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root.FullName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { assembly, mode }
        } };
        Assert.True(child.Start());
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await child.WaitForExitAsync(deadline.Token); }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill(entireProcessTree: true);
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
        }
        var output = await stdout.WaitAsync(deadline.Token);
        var error = await stderr.WaitAsync(deadline.Token);
        Assert.Equal(42, child.ExitCode);
        Assert.Contains("Unrecoverable Databento recovery", output);
        Assert.Contains("OTEL_LOCAL_SUBMISSION=", output);
        if (expectStderr)
            Assert.Contains("API ", error);
        else Assert.True(string.IsNullOrWhiteSpace(error));
    }
}
