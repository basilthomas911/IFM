using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>A closed preparation request containing exactly one cross-owner mutation.</summary>
[MessagePackObject]
public sealed record ParameterCommandPreparation(
    [property: Key(0)] AssignParameterVersionCommand? Assignment = null,
    [property: Key(1)] RetireParameterVersionCommand? Retirement = null,
    [property: Key(2)] PublishParameterVersionCommand? Publication = null,
    [property: Key(3)] ApplySignalStartupPlanCommand? Startup = null,
    [property: Key(4)] CreateParameterSetCommand? Creation = null);

/// <summary>Attaches projected evidence to the producer's original command.</summary>
public static class ParameterCommandEvidenceExtensions
{
    extension(ParameterCommandPreparation request)
    {
        public Guid OperationId => request.Assignment?.CommandId ?? request.Retirement?.CommandId
            ?? request.Publication?.CommandId ?? request.Startup?.CommandId ?? request.Creation?.CommandId ?? Guid.Empty;

        public ParameterCommandPreparation WithEvidence(ParameterCommandEvidenceReadModel result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (request.OperationId == Guid.Empty || request.OperationId != result.OperationId)
                throw new InvalidOperationException("PARAM.PREPARATION_IDENTITY_MISMATCH");
            return request switch
            {
                { Assignment: { } value } => request with { Assignment = value with { Evidence = result.Evidence } },
                { Retirement: { } value } => request with { Retirement = value with { Evidence = result.Evidence } },
                { Publication: { } value } => request with { Publication = value with { Evidence = result.Evidence } },
                { Startup: { } value } => request with { Startup = value with { Evidence = result.Evidence } },
                { Creation: { } value } => request with { Creation = value with { Evidence = result.Evidence } },
                _ => throw new InvalidOperationException("PARAM.PREPARATION_INVALID")
            };
        }
    }
}
