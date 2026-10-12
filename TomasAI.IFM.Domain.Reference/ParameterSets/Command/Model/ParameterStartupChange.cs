using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
namespace TomasAI.IFM.Domain.Reference.ParameterSets.Command.Model;
/// <summary>Immutable proposed startup run, report, or release.</summary>
internal sealed record ParameterStartupChange(long Revision, ParameterStartupRun? StartupRun = null, ParameterSignalStartupReport? StartupReport = null, bool Released = false, string? RejectionReason = null);
