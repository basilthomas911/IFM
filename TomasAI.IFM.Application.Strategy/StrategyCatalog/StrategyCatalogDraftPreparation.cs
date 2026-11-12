using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Canonical;

namespace TomasAI.IFM.Application.Strategy.StrategyCatalog;

/// <summary>Prepares draft contents before the configuration write transaction.</summary>
public static class StrategyCatalogDraftPreparation
{
    /// <summary>Canonicalizes the draft and validates its parameter values against the exact schema.</summary>
    public static async Task<PreparedStrategyCatalogDraft> PrepareAsync(
        IConfigurationDbReadContext database, StrategyCatalogDefinition definition, CancellationToken cancellationToken)
    {
        var canonical = StrategyCatalogValidation.Canonicalize(definition);
        if (canonical.Key.Kind != StrategyCatalogKind.ParameterSet)
            return new(canonical, null);

        var schema = canonical.Parent is { } key
            ? await database.GetStrategyCatalogAsync(key, cancellationToken).ConfigureAwait(false)
            : null;
        if (schema is null)
            throw new InvalidOperationException($"Missing catalog dependency: {canonical.Parent}.");
        StrategyCatalogValidation.ValidateParameters(StrategyCatalogValidation.ReadShape(schema.Definition.Settings), canonical.Settings);
        return new(canonical, schema.ContentHash);
    }

    extension(IConfigurationDbContext database)
    {
        /// <summary>Prepares and stores a draft through the non-event-sourced provisioning path.</summary>
        public async Task<string> InsertStrategyCatalogDraftAsync(StrategyCatalogDefinition definition, int expectedPreviousVersion,
            string createdBy, CancellationToken cancellationToken = default)
        {
            var prepared = await PrepareAsync(database, definition, cancellationToken).ConfigureAwait(false);
            return await database.InsertPreparedStrategyCatalogDraftAsync(prepared, expectedPreviousVersion, createdBy, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
