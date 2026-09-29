using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.ParameterSets;

/// <summary>Publishes and assigns the initial ES Market Selection defaults through the authoritative actor paths.</summary>
internal static class OptionSpreadStrategyDefaultParameterSets
{
    static readonly Definition[] Definitions =
    [
        new(StableId("parameter-set:option-spread-strategy:iron-condor-defaults"),
            "Iron Condor",
            "Per-symbol short-call, call-width, short-put, and put-width Market Selection defaults.",
            new IronCondorMarketSelectionParameterModel(), OptionSpreadStrategyParameterScopeModel.IronCondor()),
        new(StableId("parameter-set:option-spread-strategy:vertical-spread-defaults"),
            "Vertical Spreads",
            "Per-symbol short-leg delta and spread-width Market Selection defaults.",
            new VerticalSpreadMarketSelectionParameterModel(), OptionSpreadStrategyParameterScopeModel.VerticalSpread())
    ];

    internal static async ValueTask EnsureAsync(IParameterSetsApi api, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(api);
        foreach (var definition in Definitions) await EnsureAsync(api, definition, token).ConfigureAwait(false);
    }

    static async ValueTask EnsureAsync(IParameterSetsApi api, Definition definition, CancellationToken token)
    {
        var state = await StateAsync(api, definition.SetId, token);
        if (state.Versions.Length == 0)
        {
            var create = await api.CreateAsync(new()
            {
                CommandId = StableId($"parameter-command:create:{definition.Descriptor.Summary.ComponentCode}:v1"),
                EntityId = new(definition.SetId), ComponentCode = definition.Descriptor.Summary.ComponentCode,
                Name = definition.Name, Description = definition.Description,
                SchemaVersion = definition.Descriptor.Summary.SchemaVersions.Single(),
                PayloadJson = definition.Descriptor.CreateDraftPayload(definition.SetId)
            }, token).ConfigureAwait(false);
            Require(create, $"create {definition.Name}");
            state = await StateAsync(api, definition.SetId, token);
        }

        var version = state.Versions.Single(candidate => candidate.Reference.Version == 1);
        Validate(definition, version);
        if (version.Status == ParameterVersionStatus.Draft)
        {
            var publish = await api.PublishAsync(new()
            {
                CommandId = StableId($"parameter-command:publish:{definition.Descriptor.Summary.ComponentCode}:v1"),
                EntityId = new(definition.SetId), ExpectedRevision = state.Revision, Version = 1,
                ComponentCode = version.Reference.ComponentCode, Name = version.Name,
                Description = version.Description, SchemaVersion = version.SchemaVersion, PayloadJson = version.PayloadJson
            }, token).ConfigureAwait(false);
            Require(publish, $"publish {definition.Name}");
            state = await StateAsync(api, definition.SetId, token);
            version = state.Versions.Single(candidate => candidate.Reference.Version == 1);
        }
        if (version.Status != ParameterVersionStatus.Published)
            throw new InvalidOperationException($"The initial {definition.Name} version is not published.");

        var assignmentId = ParameterAssignmentPolicyModel.AssignmentId(definition.Scope);
        var assigned = await api.AssignAsync(new()
        {
            CommandId = StableId($"parameter-command:assign:{definition.Descriptor.Summary.ComponentCode}:v1"),
            EntityId = new(assignmentId), Scope = definition.Scope, Reference = version.Reference
        }, token).ConfigureAwait(false);
        Require(assigned, $"assign {definition.Name}");
    }

    static async Task<ParameterSetSnapshot> StateAsync(IParameterSetsApi api, Guid setId, CancellationToken token)
    {
        var result = await api.StateAsync(setId, token).ConfigureAwait(false);
        if (!result.Success || result.Value is null)
            throw new InvalidOperationException(result.ErrorMessage ?? "Option-spread parameter state is unavailable.");
        return result.Value;
    }

    static void Validate(Definition definition, ParameterSetVersion version)
    {
        if (version.Reference.SetId != definition.SetId
            || version.Reference.ComponentCode != definition.Descriptor.Summary.ComponentCode
            || definition.Descriptor.Validate(version.PayloadJson, version.SchemaVersion)
                .Any(issue => issue.Severity == ParameterIssueSeverity.Error))
            throw new InvalidOperationException($"The stored {definition.Name} version is invalid.");
    }

    static void Require(ServiceResult<GuidResult> result, string operation)
    { if (!result.Success) throw new InvalidOperationException(result.ErrorMessage ?? $"Could not {operation}."); }
    static Guid StableId(string value) => new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    sealed record Definition(Guid SetId, string Name, string Description,
        IParameterComponentDescriptor Descriptor, ParameterAssignmentScope Scope);
}
