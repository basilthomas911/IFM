using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Persistence;

/// <summary>Checks only infrastructure freshness; business decisions consume serialized owner evidence.</summary>
internal static class ParameterEvidenceFence
{
    internal static async Task DemandAsync(IConfigurationDbContext database, ICommand command, CancellationToken token = default)
    {
        ParameterCommandEvidence? evidence;
        switch (command)
        {
            case CreateParameterSetCommand { LegacySource: not null } creation: evidence = creation.Evidence; break;
            case AssignParameterVersionCommand assignment: evidence = assignment.Evidence; break;
            case RetireParameterVersionCommand retirement: evidence = retirement.Evidence; break;
            case PublishParameterVersionCommand publication: evidence = publication.Evidence; break;
            case ApplySignalStartupPlanCommand startup: evidence = startup.Evidence; break;
            default: return;
        }
        if (evidence is null || evidence.RevisionFence < 0) throw new InvalidOperationException("PARAM.EVIDENCE_REQUIRED");
        if (await database.GetParameterRevisionFenceAsync(token).ConfigureAwait(false) != evidence.RevisionFence)
            throw new InvalidOperationException("PARAM.EVIDENCE_STALE");
    }
}
