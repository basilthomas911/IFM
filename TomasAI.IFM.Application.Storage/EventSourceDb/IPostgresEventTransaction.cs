using System.Diagnostics;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Application.Storage.EventSourceDb.Persistence;
using System.Data;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.EventSourceDb;

/// <summary>Runs business writes and the existing event-store append on one connection/transaction.</summary>
public interface IPostgresEventTransaction
{
    /// <summary>Executes once without replaying the delegate after a confirmed rollback.</summary>
    Task<T> ExecuteOnceAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task<T> ExecuteAsync<T>(Func<EnlistedEventTransaction, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default);
}
