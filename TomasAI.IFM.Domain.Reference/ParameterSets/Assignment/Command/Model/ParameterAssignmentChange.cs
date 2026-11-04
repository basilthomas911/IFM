using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
/// <summary>Immutable proposed assignment and its audit information.</summary>
internal sealed record ParameterAssignmentChange(ParameterAssignmentRevision? ParameterAssignment, string RequestHash, ParameterAuditEntry? ParameterAudit, string? RejectionReason = null);
