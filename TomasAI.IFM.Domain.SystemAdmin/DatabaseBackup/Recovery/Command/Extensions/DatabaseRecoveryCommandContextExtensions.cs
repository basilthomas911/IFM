using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Extensions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Actor;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.SystemAdmin.DatabaseBackup.Recovery.Command.Extensions;

/// <summary>Exposes readonly DatabaseRecoveryCommand Command context properties.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "C# extension members use their declared receiver; static conversion would remove the established actor context extension contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1034", Justification = "C# 14 extension blocks generate nested implementation types; the public API consists of actor context extension members, not nested domain types.")]
public static class DatabaseRecoveryCommandContextExtensions
{
    extension(ICommandActorContext<DatabaseRecoveryCommandActor> context)
    {
        /// <summary>Gets the domain-specific typed context.</summary>
        public IDatabaseRecoveryCommandContext DomainContext =>
            IsArgumentNull.Named(context as IDatabaseRecoveryCommandContext, nameof(context))!;
        /// <summary>Gets the Supervisor service retained by the typed context.</summary>
        public IActorSupervisor Supervisor => context.DomainContext.Supervisor;
        /// <summary>Gets the EventProjector service retained by the typed context.</summary>
        public IEventProjector<DatabaseRecoveryCommandActor> EventProjector => context.DomainContext.EventProjector;
        /// <summary>Gets the Logger service retained by the typed context.</summary>
        public ILogger<DatabaseRecoveryCommandActor> Logger => context.DomainContext.Logger;
    }
}
