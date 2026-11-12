using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.EventSourcing.Persistence;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Storage.Tools;

internal static class ConfigurationBaselineServices
{
    internal static ConfigurationProjectionBaseline Create(IDbConnectionSettings settings, IPostgresEventTransaction transactions)
    {
        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        var configuration = new ConfigurationDbContext(settings, factory, NullLogger<DbProvider>.Instance);
        repositories.Add(typeof(IObjectRepository<ConfigurationDbContext>), configuration);
        return new ConfigurationProjectionBaseline(transactions, configuration);
    }
}
