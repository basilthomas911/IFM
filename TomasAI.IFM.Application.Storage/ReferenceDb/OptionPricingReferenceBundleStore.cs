using System.Text.Json;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Immutable content-addressed publication. A manifest is written only after all its exact mappings exist.</summary>
public sealed class OptionPricingReferenceBundleStore(IObjectRepository db)
{
    public const string CreateTable = "CREATE TABLE IF NOT EXISTS option_pricing_reference_bundle (bundle_id text PRIMARY KEY, payload text);";
    const string Insert = "INSERT INTO option_pricing_reference_bundle(bundle_id,payload) VALUES(:id,:payload) IF NOT EXISTS;";
    const string Select = "SELECT payload FROM option_pricing_reference_bundle WHERE bundle_id=:id;";
    public async Task PublishAsync(OptionPricingReferenceBundle bundle, CancellationToken token)
    {
        bundle.Validate();
        var mappings = new OptionPricingConventionStore(db);
        foreach (var definition in bundle.Definitions)
        {
            var mapping = await mappings.GetAsync(definition.ContractId, definition.MappingVersion, token).ConfigureAwait(false);
            if (mapping is null || mapping.DefinitionDigest != definition.DefinitionDigest || mapping.CalendarVersion != bundle.Calendar.Version)
                throw new InvalidDataException("Reference bundle contains an unpublished or conflicting mapping.");
        }
        var payload = JsonSerializer.Serialize(bundle);
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > 4 * 1024 * 1024) throw new InvalidDataException("Reference bundle is oversized.");
        await db.Use("OptionReference.Publish", Insert).SetParameters(new Parameters([bundle.BundleId, payload])).ExecuteCommandAsync(token).ConfigureAwait(false);
        var saved = await ReadAsync(bundle.BundleId, token).ConfigureAwait(false);
        if (saved?.BundleId != bundle.BundleId) throw new InvalidDataException("Reference publication was not confirmed.");
    }
    public async Task<OptionPricingReferenceBundle?> ReadAsync(string id, CancellationToken token)
    {
        if (id is null || id.Length != 64 || !id.All(Uri.IsHexDigit)) throw new ArgumentException("Exact bundle digest required.");
        var rows = await db.Use("OptionReference.Read", Select).SetParameters(new Parameters([id]))
            .ExecuteQueryAsync(row => row.GetString(0), token).ConfigureAwait(false);
        var payload = rows.SingleOrDefault();
        if (payload is null) return null;
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > 4 * 1024 * 1024) throw new InvalidDataException("Reference bundle is oversized.");
        var bundle = JsonSerializer.Deserialize<OptionPricingReferenceBundle>(payload) ?? throw new InvalidDataException("Empty reference bundle.");
        bundle.Validate();
        if (bundle.BundleId != id) throw new InvalidDataException("Reference bundle identity mismatch.");
        return bundle;
    }
    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
}
