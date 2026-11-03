using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Service;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event.Actor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Policy.Event.Extensions;

/// <summary>Exposes readonly DatabaseBackupPolicyEvent Event context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class DatabaseBackupPolicyEventContextExtensions
{
    extension(IEventActorContext<DatabaseBackupPolicyEventActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IDatabaseBackupPolicyEventContext DomainContext =>
            IsArgumentNull.Named(context as IDatabaseBackupPolicyEventContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<DatabaseBackupPolicyEventActor> Logger => context.DomainContext.Logger;
    }
}
