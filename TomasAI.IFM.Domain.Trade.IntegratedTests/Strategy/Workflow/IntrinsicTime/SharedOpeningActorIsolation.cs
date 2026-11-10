using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.IntegrationTesting;

namespace TomasAI.IFM.Domain.Trade.IntegratedTests.Strategy.Workflow.IntrinsicTime;

/// <summary>Prevents the assembly host from draining another emulator account's shared-database outboxes.</summary>
sealed class SharedOpeningActorIsolation(ISupervisorManagedActorLifecycle lifecycle) : IAsyncDisposable
{
    public static async Task<SharedOpeningActorIsolation> EnterAsync()
    {
        // Assembly-level test parallelism is disabled. The independently configured host
        // retains its own actors, broker ledger and transport while the shared actors stop.
        var services = KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>.StartedSharedServices
            ?? throw new InvalidOperationException("The assembly host must be initialized before opening qualification.");
        var lifecycle = services.GetRequiredService<ISupervisorManagedActorLifecycle>();
        var stopped = await lifecycle.ShutdownActorsAsync(CancellationToken.None);
        stopped.Succeeded.Should().BeTrue(stopped.FailureReason);
        return new(lifecycle);
    }

    public async ValueTask DisposeAsync()
    {
        var restarted = await lifecycle.StartupActorsAsync(CancellationToken.None);
        restarted.Succeeded.Should().BeTrue(restarted.FailureReason);
    }
}
