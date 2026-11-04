using System.Diagnostics;
using FluentAssertions;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

/// <summary>Real API composition-root verification; deliberately no DB initialization or actor/feed startup.</summary>
public sealed class ApiStartupVerificationProcessTests
{
    [Theory]
    [InlineData("Development", null)]
    [InlineData("Production", "DatabentoLive")]
    [InlineData("Production", "Synthetic")]
    [Trait("Category", "StartupVerification")]
    public async Task Api_container_verifies_without_initializing_data_or_starting_services(string environment, string? dataSource)
    {
        var serverDirectory = AppContext.BaseDirectory;
        using var deployment = new TomasAI.IFM.IntegrationTesting.ApiProcessDeploymentManifest(serverDirectory);
        var serverAssembly = Path.Combine(serverDirectory, "TomasAI.IFM.Application.Api.Server.dll");
        File.Exists(serverAssembly).Should().BeTrue("the real API host must be built");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = serverDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(serverAssembly);
        deployment.Configure(start);
        start.ArgumentList.Add("--verify-startup-only");
        // Verifier must take precedence over this otherwise-mutating bootstrap switch.
        start.ArgumentList.Add("--bootstrap-trade-strategy-families-only");
        start.Environment["DOTNET_ENVIRONMENT"] = environment;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        // Feed persistence policy belongs to Market Data Host; API verification resolves its remote client.
        start.Environment.Remove("AppSettings__Databento__DeploymentProfile");
        // Verify that local feed settings do not cause the API to construct feed workers.
        if (dataSource is null)
            start.Environment.Remove("AppSettings__Databento__DataSource");
        else
            start.Environment["AppSettings__Databento__DataSource"] = dataSource;
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
        process.ExitCode.Should().Be(0, output);
        output.Should().NotContain("TradeStrategyFamily bootstrap-only process completed.");
        output.Should().Contain("IFM startup verification completed; no schemas, actors, feeds or HTTP listeners started.").And.NotContain("startup failed");
    }
}
