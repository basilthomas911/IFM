using Microsoft.Extensions.DependencyInjection;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;

internal static class SupervisorTestLifecycle
{
    internal static async ValueTask ShutdownAsync(IServiceProvider services)
    {
        var managed = await services.GetRequiredService<ISupervisorManagedActorLifecycle>()
            .ShutdownActorsAsync(CancellationToken.None);
        if (!managed.Succeeded)
            throw new InvalidOperationException(
                $"Managed actor test shutdown failed at {managed.Stage}: {managed.FailureReason}");

        var supervisor = await services.GetRequiredService<ISupervisorBootstrap>()
            .StopSupervisorAsync(CancellationToken.None);
        if (!supervisor.Succeeded)
            throw new InvalidOperationException(
                $"Supervisor test shutdown failed at {supervisor.Stage}: {supervisor.FailureReason}");
    }
}
