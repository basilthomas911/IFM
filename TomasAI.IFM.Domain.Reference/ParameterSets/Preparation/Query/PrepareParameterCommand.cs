using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Model;
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

/// <summary>Reads coherent owner state under the writer lease and serializes the evidence into the command.</summary>
public static class PrepareParameterCommand
{
    extension(PrepareParameterCommandQuery query)
    {
        public async ValueTask ExecuteAsync(IParameterPreparationQueryContext context, ILogger<ParameterPreparationQueryActor> logger, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(logger);
            context.AccessPolicy.Demand(ParameterCapability.Read);
            var request = query.Preparation;
            if (new object?[] { request.Assignment, request.Retirement, request.Publication, request.Startup, request.Creation }.Count(value => value is not null) != 1)
                throw new InvalidOperationException("PARAM.PREPARATION_INVALID");

            ParameterCommandPreparation prepared;
            var lease = await context.ConfigurationDb.AcquireParameterWriteLeaseAsync(token).ConfigureAwait(false);
            await using (lease.ConfigureAwait(false))
            {
                var fence = await context.ConfigurationDb.GetParameterRevisionFenceAsync(token).ConfigureAwait(false);
                if (request.Creation is { } creation)
                {
                    var existing = await context.ParameterSets.LoadStateAsync(creation, token).ConfigureAwait(false);
                    if (existing.Operations.ContainsKey(creation.CommandId))
                        prepared = request with { Creation = creation with { Evidence = new(fence) } };
                    else
                    {
                        var source = creation.LegacySource ?? throw new InvalidOperationException("PARAM.PREPARATION_INVALID");
                        var legacy = (await context.ConfigurationDb.GetLegacyParameterVersionsAsync(source.SetId, source.Version, cancellationToken: token).ConfigureAwait(false))
                            .SingleOrDefault() ?? throw new InvalidOperationException("PARAM.LEGACY_NOT_FOUND");
                        prepared = request with { Creation = creation with { Evidence = new(fence, LegacyVersion: legacy) } };
                    }
                }
                else if (request.Assignment is { } assignment)
                {
                    var version = await ReadVersion(context, assignment.Reference.SetId, assignment.Reference.Version, token).ConfigureAwait(false);
                    if (version.Reference != assignment.Reference) throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
                    prepared = request with { Assignment = assignment with { Evidence = new(fence, ExactVersion: version) } };
                }
                else if (request.Retirement is { } retirement)
                {
                    var version = await ReadVersion(context, retirement.EntityId.SetId, retirement.Version, token).ConfigureAwait(false);
                    var assignments = await ParameterStartupReader.ReadAssignmentsUnderLeaseAsync(context.Assignments, token).ConfigureAwait(false);
                    prepared = request with { Retirement = retirement with { Evidence = new(fence, ExactVersion: version,
                        HasActiveAssignments: assignments.Any(assignment => assignment.Enabled && assignment.Reference == version.Reference)) } };
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
                    prepared = request with { Publication = publication with { Evidence = new(fence, ExactVersion: version, Catalog: catalog) } };
                }
                else
                {
                    var startup = request.Startup!;
                    var state = await context.Startups.LoadStateAsync(startup, token).ConfigureAwait(false);
                    if (state.RunId == startup.RunId && state.RunId != Guid.Empty)
                        prepared = request with { Startup = startup with { Evidence = new(fence, Assignments: [], Versions: []) } };
                    else
                    {
                        var snapshot = await ParameterStartupReader.ReadUnderLeaseAsync(startup.RunId, context.Assignments, context.ParameterSets, token).ConfigureAwait(false);
                        prepared = request with { Startup = startup with { Evidence = new(fence,
                            Assignments: snapshot.Scopes.ToImmutableArray(), Versions: snapshot.Assignments.Select(item => Freeze(item.Version)).DistinctBy(version => version.Reference).ToImmutableArray()) } };
                    }
                }
            }
            await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb, new ServiceOk<ParameterCommandPreparation>(prepared)).ConfigureAwait(false);
        }
    }

    static async Task<ParameterSetVersion> ReadVersion(IParameterPreparationQueryContext context, Guid setId, int version, CancellationToken token)
    {
        var identity = new ParameterSetEntityId(setId);
        var address = new CreateParameterSetCommand { EntityId = identity,
            Subject = new(ActorType.Command, CreateParameterSetCommand.Actor, CreateParameterSetCommand.Verb, identity.Format()) };
        var state = await context.ParameterSets.LoadStateAsync(address, token).ConfigureAwait(false);
        return state.Versions.TryGetValue(version, out var selected) ? Freeze(selected) : throw new InvalidOperationException("PARAM.EXACT_VERSION_MISSING");
    }

    static ParameterSetVersion Freeze(ParameterSetVersion version)
        => version with { Parameters = ParameterValueCodec.Snapshot(version.Values()) };
}
