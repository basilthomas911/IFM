using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.SecuritiesDb;

namespace TomasAI.IFM.Application.Storage.OptionPricing;

/// <summary>Immutable content-addressed publication. A manifest is written only after all its exact mappings exist.</summary>
/// <param name="db">The single SecuritiesDb owner of versions and manifests.</param>
public sealed class OptionPricingReferenceBundleStore(ISecuritiesDbContext db)
{
    /// <summary>Publishes a validated manifest only after its reviewed SecuritiesDb versions are available.</summary>
    /// <param name="bundle">The sealed content-addressed manifest.</param>
    /// <param name="token">Cancels publication.</param>
    /// <returns>The publication and confirmation operation.</returns>
    public async Task PublishAsync(OptionPricingReferenceBundle bundle, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        bundle.Validate();
        var mappings = db.DbReader;
        foreach (var definition in bundle.Definitions)
        {
            var mapping = await mappings.GetAsync(definition.ContractId, definition.MappingVersion, token).ConfigureAwait(false);
            if (mapping is null || !string.Equals(mapping.DefinitionDigest,definition.DefinitionDigest,StringComparison.Ordinal) || !string.Equals(mapping.CalendarVersion,bundle.Calendar.Version,StringComparison.Ordinal))
                throw new InvalidDataException("Reference bundle contains an unpublished or conflicting mapping.");
        }
        var payload = JsonSerializer.Serialize(bundle);
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > 4 * 1024 * 1024) throw new InvalidDataException("Reference bundle is oversized.");
        await db.DbWriter.InsertOptionPricingReferenceBundleAsync(bundle.BundleId, payload, token).ConfigureAwait(false);
        var saved = await ReadAsync(bundle.BundleId, token).ConfigureAwait(false);
        if (!string.Equals(saved?.BundleId,bundle.BundleId,StringComparison.Ordinal)) throw new InvalidDataException("Reference publication was not confirmed.");
    }
    /// <summary>Loads and validates a saved manifest without consulting retired ReferenceDb tables.</summary>
    /// <param name="id">The exact manifest content digest.</param>
    /// <param name="token">Cancels the read.</param>
    /// <returns>The validated manifest, or null when absent.</returns>
    public async Task<OptionPricingReferenceBundle?> ReadAsync(string id, CancellationToken token)
    {
        if (id is null || id.Length != 64 || !id.All(Uri.IsHexDigit)) throw new ArgumentException("Exact bundle digest required.",nameof(id));
        var payload = await db.DbReader.GetOptionPricingReferenceBundlePayloadAsync(id, token).ConfigureAwait(false);
        if (payload is null) return null;
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > 4 * 1024 * 1024) throw new InvalidDataException("Reference bundle is oversized.");
        var bundle = JsonSerializer.Deserialize<OptionPricingReferenceBundle>(payload) ?? throw new InvalidDataException("Empty reference bundle.");
        bundle.Validate();
        if (!string.Equals(bundle.BundleId,id,StringComparison.Ordinal)) throw new InvalidDataException("Reference bundle identity mismatch.");
        return bundle;
    }
}
