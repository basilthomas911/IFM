using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;

/// <summary>Immutable proposed parameter version and its audit information.</summary>
internal sealed record ParameterVersionChange(ParameterSetVersion? ParameterVersion, long CatalogRevision, string RequestHash, string ParameterAuditJson, string? RejectionReason = null);
/// <summary>Immutable proposed assignment and its audit information.</summary>
internal sealed record ParameterAssignmentChange(ParameterAssignmentRevision? ParameterAssignment, string RequestHash, string ParameterAuditJson, string? RejectionReason = null);
/// <summary>Immutable proposed startup run, report, or release.</summary>
internal sealed record ParameterStartupChange(long Revision, ParameterStartupRun? StartupRun = null, ParameterSignalStartupReport? StartupReport = null, bool Released = false, string? RejectionReason = null);
