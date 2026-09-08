using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb;

/// <summary>Immutable reviewed per-contract mappings, separate from exact raw instrument definitions.</summary>
public sealed class OptionPricingConventionStore(IObjectRepository db) : IOptionPricingConventionStore
{
    public const string CreateTable = """
        CREATE TABLE IF NOT EXISTS option_pricing_convention (
          contract_id text, mapping_version text, payload blob,
          PRIMARY KEY ((contract_id),mapping_version));
        """;
    const string Select = "SELECT payload FROM option_pricing_convention WHERE contract_id=:contract AND mapping_version=:version;";
    const string Insert = "INSERT INTO option_pricing_convention(contract_id,mapping_version,payload) VALUES(:contract,:version,:payload) IF NOT EXISTS;";

    public async Task<OptionPricingConvention?> GetAsync(string contractId, string mappingVersion, CancellationToken cancellationToken)
    {
        ValidateKey(contractId, mappingVersion);
        var rows = await db.Use("OptionPricingConvention.ByVersion", Select)
            .SetParameters(new Parameters([contractId, mappingVersion]))
            .ExecuteQueryAsync(row => MessagePackBinarySerializer.Shared.Deserialize<OptionPricingConvention>(row.GetBytes(0)), cancellationToken).ConfigureAwait(false);
        var result = rows.SingleOrDefault();
        if (result is not null && (result.ContractId != contractId || result.MappingVersion != mappingVersion || result.SchemaVersion != 1))
            throw new InvalidDataException("Stored option convention identity/schema differs from its key.");
        return result;
    }

    /// <summary>Publishes reviewed content once; identical retries succeed and conflicting versions cannot overwrite.</summary>
    public async Task InsertReviewedAsync(OptionPricingConvention value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateKey(value.ContractId, value.MappingVersion);
        var failure = OptionPricingQualification.Validate(value, value.EffectiveFromUtc);
        if (failure is not null && failure.Code != "PricingModelUnsupported")
            throw new ArgumentException(failure.Code, nameof(value));
        if (MessagePackBinarySerializer.MeasureContent(value) > 32768)
            throw new ArgumentException("Option convention exceeds the content limit.", nameof(value));
        await db.Use("OptionPricingConvention.InsertReviewed", Insert)
            .SetParameters(new Parameters([value.ContractId, value.MappingVersion, MessagePackBinarySerializer.Shared.Serialize(value)!]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        var stored = await GetAsync(value.ContractId, value.MappingVersion, cancellationToken).ConfigureAwait(false);
        if (stored != value) throw new InvalidOperationException("Option convention version conflicts with published content.");
    }

    static void ValidateKey(string contract, string version)
    {
        if (string.IsNullOrWhiteSpace(contract) || contract.Length > 128
            || string.IsNullOrWhiteSpace(version) || version.Length > 128)
            throw new ArgumentException("Bounded exact contract and mapping version are required.");
    }
    readonly record struct Parameters(object[] Values) : IBindValue { public object Bind() => Values; }
}
