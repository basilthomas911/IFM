using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Query;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Query.Actor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Retention.Query.Extensions;

/// <summary>Exposes readonly DatabaseBackupRetentionQuery Query context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class DatabaseBackupRetentionQueryContextExtensions
{
    extension(IQueryActorContext<DatabaseBackupRetentionQueryActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IDatabaseBackupRetentionQueryContext DomainContext =>
            IsArgumentNull.Named(context as IDatabaseBackupRetentionQueryContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the DbContext service retained by the typed context.</summary>
        public ISystemAdminDbContext DbContext => context.DomainContext.DbContext;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<DatabaseBackupRetentionQueryActor> Logger => context.DomainContext.Logger;
    }
}
