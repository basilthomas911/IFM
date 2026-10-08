using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
namespace TomasAI.IFM.Domain.SystemAdmin.ScheduledTask.Command.Model;
/// <summary>Contains the accepted ScheduledTask calculation or a business rejection.</summary>
public sealed record ScheduledTaskChange(ScheduledTaskDefinition? ScheduledTaskDefinition, string RejectionReason = "")
{
    /// <summary>Gets whether the calculation produced a valid change.</summary>
    public bool Accepted => ScheduledTaskDefinition is not null && RejectionReason.Length == 0;
    /// <summary>Checks the calculated aggregate identity before applying its event.</summary>
    public bool IsValidFor(ScheduledTaskId identity) => Accepted && ScheduledTaskDefinition!.Id == identity && ScheduledTaskDefinition.Revision > 0;
}
