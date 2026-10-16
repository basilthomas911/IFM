using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;

/// <summary>Immutable proposed parameter version and its audit information.</summary>
internal sealed record ParameterVersionChange(ParameterSetVersion? ParameterVersion, long CatalogRevision, string RequestHash, ParameterAuditEntry? ParameterAudit, string? RejectionReason = null);
