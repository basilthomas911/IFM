using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.ServiceApi;
namespace TomasAI.IFM.Application.Api.Nats.Client;
/// <summary>Registers the typed scheduled-task APIs for UI and scheduler-host clients.</summary>
public static class ScheduledTaskApiServiceCollectionExtensions
{
    /// <summary>Adds actor transport facades without registering a second scheduler engine.</summary>
    public static IServiceCollection AddScheduledTaskNatsClientApis(this IServiceCollection services)
    {
        services.TryAddScoped<IScheduledTaskCommandApi, ScheduledTaskCommandApi>();
        services.TryAddScoped<IScheduledTaskQueryApi, ScheduledTaskQueryApi>();
        return services;
    }
}
