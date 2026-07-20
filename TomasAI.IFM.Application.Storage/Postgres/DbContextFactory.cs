using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;

namespace TomasAI.IFM.Application.Storage.Postgres;

/// <summary>
/// dbcontext factory
/// </summary>
public class DbContextFactory : IDbContextFactory
{
    readonly IDbContextResolver _dbContextResolver;

    /// <summary>
    /// dbcontext factory constructor
    /// </summary>
    /// <param name="dbContextResolver"></param>
    public DbContextFactory(IDbContextResolver dbContextResolver)
    {
        _dbContextResolver = dbContextResolver;
    }

    // dbcontext properties
    public IObjectRepository<EventSourceDbContext> EventSourceDb => _dbContextResolver.Resolve<EventSourceDbContext>();
    public IObjectRepository<SequenceIdDbContext> SequenceIdDb => _dbContextResolver.Resolve<SequenceIdDbContext>();
    public IObjectRepository<LogDbContext> LogDb => _dbContextResolver.Resolve<LogDbContext>();

}
