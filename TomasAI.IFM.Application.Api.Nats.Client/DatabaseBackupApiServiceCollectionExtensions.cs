using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public static class DatabaseBackupApiServiceCollectionExtensions
{

    extension(IServiceCollection services)
    {
        public IServiceCollection AddDatabaseBackupNatsClientApis()
        {
            ArgumentNullException.ThrowIfNull(services);
            services.TryAddScoped<IDatabaseBackupCommandApi, DatabaseBackupCommandApi>();
            services.TryAddScoped<IDatabaseBackupQueryApi, DatabaseBackupQueryApi>();
            return services;
        }
    }
}
