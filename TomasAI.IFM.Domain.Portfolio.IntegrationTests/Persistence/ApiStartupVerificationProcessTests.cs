using System.Diagnostics;
using FluentAssertions;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

/// <summary>Real API composition-root verification; deliberately no DB initialization or actor/feed startup.</summary>
public sealed class ApiStartupVerificationProcessTests
{
    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("Production", "DatabentoLive", true)]
    [InlineData("Production", "Synthetic", false)]
    [Trait("Category", "StartupVerification")]
    public async Task Api_container_verifies_without_initializing_data_or_starting_services(string environment, string? dataSource, bool validConfiguration)
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Parent!.Parent!.Parent!.FullName;
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var serverDirectory = Path.Combine(repositoryRoot, "TomasAI.IFM.Application.Api.Server", "bin", configuration, "net10.0");
        var serverAssembly = Path.Combine(serverDirectory, "TomasAI.IFM.Application.Api.Server.dll");
        File.Exists(serverAssembly).Should().BeTrue("the real API host must be built");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = serverDirectory, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
        };
        start.ArgumentList.Add(serverAssembly);
        start.ArgumentList.Add("--verify-startup-only");
        // Verifier must take precedence over this otherwise-mutating bootstrap switch.
        start.ArgumentList.Add("--bootstrap-trade-strategy-families-only");
        start.Environment["DOTNET_ENVIRONMENT"] = environment;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        // Production must explicitly configure a valid feed source; verification must
        // not disable the existing synthetic-persistence isolation guard.
        if (dataSource is not null) start.Environment["AppSettings__Databento__DataSource"] = dataSource;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start API verifier.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true); // Only the verifier owned by this test.
            await process.WaitForExitAsync();
            throw;
        }
        var output = $"{await stdout}{await stderr}";
        process.ExitCode.Should().Be(validConfiguration ? 0 : 1, output);
        output.Should().NotContain("TradeStrategyFamily bootstrap-only process completed.");
        if (validConfiguration)
            output.Should().Contain("IFM startup verification completed; no schemas, actors, feeds or HTTP listeners started.").And.NotContain("startup failed");
        else
            output.Should().Contain("The synthetic Databento feed may persist snapshots only under the SyntheticCi deployment profile.")
                .And.NotContain("IFM startup verification completed");
    }
}
