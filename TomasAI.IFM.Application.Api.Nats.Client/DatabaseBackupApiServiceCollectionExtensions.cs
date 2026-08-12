using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public static class DatabaseBackupApiServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseBackupNatsClientApis(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<IDatabaseBackupCommandApi, DatabaseBackupCommandApi>();
        services.TryAddScoped<IDatabaseBackupQueryApi, DatabaseBackupQueryApi>();
        return services;
    }
}
