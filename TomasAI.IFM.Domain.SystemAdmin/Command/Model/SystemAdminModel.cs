using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.SystemAdmin.Events;

namespace TomasAI.IFM.Domain.SystemAdmin.Command.Model;

/// <summary>
/// Provides command-to-event mapping helpers for SystemAdmin command handling.
/// </summary>
/// <remarks>
/// This type centralises translation from system admin commands into their corresponding domain events,
/// preserving a consistent event construction pattern across handlers.
/// <para>
/// Each factory method creates a fully-populated event instance using command metadata such as subject,
/// actor identifiers, originator details, and business payload fields.
/// </para>
/// <para>
/// These helpers are intended to be consumed by command handlers before calling state update logic.
/// </para>
/// </remarks>
internal static class SystemAdminModel
{
   
}
