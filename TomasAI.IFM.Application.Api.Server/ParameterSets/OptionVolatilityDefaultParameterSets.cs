using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Reference.ParameterSets.Model;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Server.ParameterSets;

/// <summary>Provisions reviewable Option Volatility drafts without publishing or assigning them.</summary>
internal static class OptionVolatilityDefaultParameterSets
{
    internal static readonly Guid SeriesSetId = StableId("parameter-set:option-volatility:series:es-atm-30d");
    internal static readonly Guid ConsumerRulesSetId = StableId("parameter-set:option-volatility:consumer-rules:default");
    internal static readonly Guid RetentionSetId = StableId("parameter-set:option-volatility:retention:default");

    static readonly Definition[] Definitions =
    [
        new(
            SeriesSetId,
            StableId("parameter-command:create:option-volatility:series:es-atm-30d:v1"),
            "ES ATM 30D Option Volatility",
            "Candidate ES ATM 30-calendar-day comparable volatility series for owner review.",
            new OptionVolatilitySeriesParameterModel()),
        new(
            ConsumerRulesSetId,
            StableId("parameter-command:create:option-volatility:consumer-rules:default:v1"),
            "Option Volatility Consumer Rules",
            "Candidate safeguards and optional consumer rules for owner review.",
            new OptionVolatilityConsumerRulesParameterModel()),
        new(
            RetentionSetId,
            StableId("parameter-command:create:option-volatility:retention:default:v1"),
            "Option Volatility Retention",
            "Candidate retention, evidence, and backfill policy for owner review.",
            new OptionVolatilityRetentionParameterModel())
    ];

    internal static async ValueTask EnsureAsync(IParameterSetsApi parameterSets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(parameterSets);
        foreach (var definition in Definitions)
            await EnsureAsync(parameterSets, definition, cancellationToken).ConfigureAwait(false);
    }

    static async ValueTask EnsureAsync(
        IParameterSetsApi parameterSets,
        Definition definition,
        CancellationToken cancellationToken)
    {
        var existing = await parameterSets.StateAsync(definition.SetId, cancellationToken).ConfigureAwait(false);
        if (!existing.Success || existing.Value is null)
            throw new InvalidOperationException(existing.ErrorMessage ?? $"The {definition.Name} parameter-set state is unavailable.");
        if (existing.Value.Versions.Length != 0)
        {
            foreach (var storedVersion in existing.Value.Versions.OrderBy(candidate => candidate.Reference.Version))
                Validate(definition, storedVersion, false);
            return;
        }

        var payload = definition.Descriptor.CreateDraftPayload(definition.SetId);
        var created = await parameterSets.CreateAsync(new CreateParameterSetCommand
        {
            CommandId = definition.CommandId,
            EntityId = new(definition.SetId),
            ComponentCode = definition.Descriptor.Summary.ComponentCode,
            Name = definition.Name,
            Description = definition.Description,
            SchemaVersion = definition.Descriptor.Summary.SchemaVersions.Single(),
            PayloadJson = payload
        }, cancellationToken).ConfigureAwait(false);
        if (!created.Success)
            throw new InvalidOperationException(created.ErrorMessage ?? $"The {definition.Name} parameter set could not be created.");

        var saved = await parameterSets.StateAsync(definition.SetId, cancellationToken).ConfigureAwait(false);
        var version = saved.Success
            ? saved.Value?.Versions.SingleOrDefault(candidate => candidate.Reference.Version == 1)
            : null;
        if (version is null)
            throw new InvalidOperationException($"The created {definition.Name} version is unavailable.");
        Validate(definition, version, true);
    }

    static void Validate(Definition definition, ParameterSetVersion version, bool requireInitialDraft)
    {
        var summary = definition.Descriptor.Summary;
        if (version.Reference.SetId != definition.SetId
            || version.Reference.ComponentCode != summary.ComponentCode
            || !summary.SchemaVersions.Contains(version.SchemaVersion))
            throw new InvalidOperationException($"The stored {definition.Name} identity or schema is invalid.");
        if (definition.Descriptor.Validate(version.PayloadJson, version.SchemaVersion).Length != 0)
            throw new InvalidOperationException($"The stored {definition.Name} payload is invalid.");
        if (!ParameterSchemaRegistry.Default.CanEditLosslessly(
                summary.ComponentCode, version.SchemaVersion, version.PayloadJson))
            throw new InvalidOperationException($"The stored {definition.Name} payload contains an unsupported field or shape.");

        using var payload = System.Text.Json.JsonDocument.Parse(version.PayloadJson);
        if (payload.RootElement.GetProperty("ParameterSetId").GetGuid() != definition.SetId
            || payload.RootElement.GetProperty("Version").GetInt32() != version.Reference.Version)
            throw new InvalidOperationException($"The stored {definition.Name} payload identity or version is invalid.");
        if (!requireInitialDraft) return;
        if (version.Reference.Version != 1 || version.Status != ParameterVersionStatus.Draft)
            throw new InvalidOperationException($"The initial {definition.Name} version must be a version-one Draft.");
        if (ParameterCanonicalPayloadModel.Hash(version.PayloadJson)
            != ParameterCanonicalPayloadModel.Hash(definition.Descriptor.CreateDraftPayload(definition.SetId)))
            throw new InvalidOperationException($"The initial {definition.Name} defaults do not match the required baseline.");
    }

    static Guid StableId(string value) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));

    sealed record Definition(
        Guid SetId,
        Guid CommandId,
        string Name,
        string Description,
        IParameterComponentDescriptor Descriptor);
}
