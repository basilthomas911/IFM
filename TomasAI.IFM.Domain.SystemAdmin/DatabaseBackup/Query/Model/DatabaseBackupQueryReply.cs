using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Actor;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Query.Model;

/// <summary>Creates consistent typed responses for Database Backup query handlers.</summary>
internal static class DatabaseBackupQueryReply
{
    /// <summary>Replies with a required projection result.</summary>
    internal static ValueTask RequiredAsync<TQuery, TResult>(IQueryActorContext<DatabaseBackupQueryActor> context, TQuery query, TResult result)
        where TQuery : DatabaseBackupQuery, IQuery<TResult> where TResult : class
        => context.ReplyAsync(query.Subject.ThreadId, query.Verb, new ServiceOk<TResult>(result));

    /// <summary>Replies with an optional projection result or a typed not-found failure.</summary>
    internal static ValueTask OptionalAsync<TQuery, TResult>(IQueryActorContext<DatabaseBackupQueryActor> context, TQuery query, TResult? result)
        where TQuery : DatabaseBackupQuery, IQuery<TResult> where TResult : class
        => context.ReplyAsync<TResult>(query.Subject.ThreadId, query.Verb,
            result is null ? new ServiceFailed<TResult>(404, "DatabaseBackup projection was not found.") : new ServiceOk<TResult>(result));
}