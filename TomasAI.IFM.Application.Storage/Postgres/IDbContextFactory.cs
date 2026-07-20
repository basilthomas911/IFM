using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage.Postgres.EventSourceDb;
using TomasAI.IFM.Application.Storage.Postgres.SequenceIdDb;
using TomasAI.IFM.Application.Storage.Postgres.LogDb;

namespace TomasAI.IFM.Application.Storage.Postgres;

public interface IDbContextFactory
{
    IObjectRepository<EventSourceDbContext> EventSourceDb { get; }
    IObjectRepository<SequenceIdDbContext> SequenceIdDb { get; }
    IObjectRepository<LogDbContext> LogDb { get; }
}
