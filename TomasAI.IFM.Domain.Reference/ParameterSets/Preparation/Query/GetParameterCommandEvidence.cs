using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Model;
using TomasAI.IFM.Domain.Reference.ParameterSets.Assignment.Command.Model;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache;
using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Actor;
using TomasAI.IFM.Domain.Reference.ParameterSets.Startup.Command.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query;

/// <summary>Reads projected evidence without rewriting the producer's command.</summary>
public static class GetParameterCommandEvidence
{
    extension(GetParameterCommandEvidenceQuery query)
    {
        public ValueTask<ParameterCommandEvidenceReadModel> ExecuteAsync(IParameterPreparationQueryContext context,
            ILogger<ParameterPreparationQueryActor> logger, CancellationToken token)
            => ReadAsync(query.Preparation, context, logger, token);
    }

    internal static async ValueTask<ParameterCommandEvidenceReadModel> ReadAsync(ParameterCommandPreparation request,
        IParameterPreparationQueryContext context, ILogger<ParameterPreparationQueryActor> logger, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(logger);
        context.AccessPolicy.Demand(ParameterCapability.Read);
        if (new object?[] { request.Assignment, request.Retirement, request.Publication, request.Startup, request.Creation }.Count(value => value is not null) != 1)
            throw new InvalidOperationException("PARAM.PREPARATION_INVALID");

        ParameterCommandEvidence evidence;
        var fence = await context.ConfigurationDb.GetProjectedParameterRevisionAsync(token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("PARAM.PROJECTION_LAG");
        if (request.Creation is { } creation)
        {
            var existing = await context.ConfigurationDb.GetParameterSetSnapshotAsync(creation.EntityId.SetId, token).ConfigureAwait(false);
            if (existing.Operations.Any(operation => operation.OperationId == creation.CommandId))
                evidence = new(fence);
            else
            {
                var source = creation.LegacySource ?? throw new InvalidOperationException("PARAM.PREPARATION_INVALID");
                var legacy = (await context.ConfigurationDb.GetLegacyParameterVersionsAsync(source.SetId, source.Version, cancellationToken: token).ConfigureAwait(false))
                    .SingleOrDefault() ?? throw new InvalidOperationException("PARAM.LEGACY_NOT_FOUND");
                evidence = new(fence, LegacyVersion: legacy);
            }
        }
        else if (request.Assignment is { } assignment)
        {
            var version = await ReadVersion(context, assignment.Reference.SetId, assignment.Reference.Version, token).ConfigureAwait(false);
            if (version.Reference != assignment.Reference) throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
            evidence = new(fence, ExactVersion: version);
        }
        else if (request.Retirement is { } retirement)
        {
            var version = await ReadVersion(context, retirement.EntityId.SetId, retirement.Version, token).ConfigureAwait(false);
            var (assignments, _) = await ReadStartupInputsAsync(context, token).ConfigureAwait(false);
            evidence = new(fence, ExactVersion: version,
                HasActiveAssignments: assignments.Any(assignment => assignment.Enabled && assignment.Reference == version.Reference));
        }
        else if (request.Publication is { } publication)
        {
            var version = await ReadVersion(context, publication.EntityId.SetId, publication.Version, token).ConfigureAwait(false);
            ParameterCatalogEvidence? catalog = null;
            if (version.Reference.ComponentCode == ParameterSchemaRegistry.StrategyOptionChainCacheComponent)
            {
                var policy = version.Values().Get<StrategyOptionChainParameterSet>();
                if (policy.Enabled)
                {
                    var definition = await context.ConfigurationDb.GetStrategyCatalogAsync(new(StrategyCatalogKind.Structure,
                        policy.StrategyDefinitionId, policy.StrategyDefinitionVersion), token).ConfigureAwait(false);
                    catalog = new(policy.StrategyDefinitionId, policy.StrategyDefinitionVersion, definition?.ContentHash ?? "",
                        definition?.Status == CatalogLifecycleStatus.Published,
                        definition?.Definition.Capabilities.Any(capability => capability.Role == "builder" && capability.Version == 1
                            && capability.Code is "IronCondor" or "CallVertical" or "PutVertical") == true);
                }
            }
            evidence = new(fence, ExactVersion: version, Catalog: catalog);
        }
        else
        {
            var startup = request.Startup!;
            if (startup.RunId != Guid.Empty && await context.ConfigurationDb.HasParameterStartupProjectionAsync(startup.RunId, token).ConfigureAwait(false))
                evidence = new(fence, Assignments: [], Versions: []);
            else
            {
                var (assignments, versions) = await ReadStartupInputsAsync(context, token).ConfigureAwait(false);
                var snapshot = new ParameterStartupSnapshotModel(startup.RunId, assignments, versions.ToDictionary(version => version.Reference));
                evidence = new(fence,
                    Assignments: snapshot.Scopes.ToImmutableArray(), Versions: snapshot.Assignments.Select(item => Freeze(item.Version)).DistinctBy(version => version.Reference).ToImmutableArray());
            }
        }
        var endingFence = await context.ConfigurationDb.GetProjectedParameterRevisionAsync(token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("PARAM.PROJECTION_LAG");
        if (endingFence != fence)
            throw new InvalidOperationException("PARAM.EVIDENCE_STALE");
        return new(request.OperationId, evidence);
    }

    static async Task<ParameterSetVersion> ReadVersion(IParameterPreparationQueryContext context, Guid setId, int version, CancellationToken token)
    {
        var snapshot = await context.ConfigurationDb.GetParameterSetSnapshotAsync(setId, token).ConfigureAwait(false);
        var selected = snapshot.Versions.SingleOrDefault(candidate => candidate.Reference.Version == version)
            ?? throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
        return Freeze(selected);
    }

    static Task<ParameterStartupInputsReadModel> ReadStartupInputsAsync(
        IParameterPreparationQueryContext context, CancellationToken token)
    {
        var ids = ParameterStartupReader.SupportedScopes().Select(ParameterAssignmentPolicyModel.AssignmentId).ToArray();
        return context.ConfigurationDb.GetParameterStartupInputsAsync(ids, token);
    }

    static ParameterSetVersion Freeze(ParameterSetVersion version)
        => version with { Parameters = ParameterValueCodec.Snapshot(version.Values()) };
}
