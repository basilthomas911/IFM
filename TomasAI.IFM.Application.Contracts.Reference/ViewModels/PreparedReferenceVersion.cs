using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.MarketData.Pricing;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

/// <summary>Contains the qualified provider identity and immutable content prepared for staging.</summary>
public sealed class PreparedReferenceVersion
{
    PreparedReferenceVersion(string providerKey, string identity, PendingReferenceVersion reference, ReferenceContractVersion value)
    {
        ProviderKey = providerKey;
        Identity = identity;
        Reference = reference;
        Value = value;
    }

    public string ProviderKey { get; }
    public string Identity { get; }
    public PendingReferenceVersion Reference { get; }
    public ReferenceContractVersion Value { get; }

    public static PreparedReferenceVersion Prepare(FuturesContractV3ReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = ReferencePayloadCodec.Write(value);
        var identity = JsonSerializer.Serialize(new
        {
            value.ContractId,
            value.Dataset,
            value.PublisherId,
            value.InstrumentId,
            value.Symbol,
            value.LastTradeDate,
            value.SecurityType
        });
        return Prepare(value.ContractId, value.Dataset, value.PublisherId, value.InstrumentId,
            value.MappingVersion, value.ReviewState, identity,
            new(value with { OnTheRun = false, Rollover = false }, null, null));
    }

    public static PreparedReferenceVersion Prepare(FuturesOptionContractReadModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = ReferencePayloadCodec.Write(value);
        var identity = OptionReferenceIdentity.Serialize(value);
        var convention = value.ReviewState == ReferenceReviewState.Reviewed ? ReviewedOptionConvention.From(value) : null;
        return Prepare(value.ContractId, value.Dataset, value.PublisherId, value.InstrumentId, value.MappingVersion,
            value.ReviewState, identity, new(null, value, convention));
    }

    static PreparedReferenceVersion Prepare(string contractId, string? dataset, ushort? publisherId, uint? instrumentId,
        string? version, ReferenceReviewState reviewState, string identity, ReferenceContractVersion value)
    {
        if (string.IsNullOrWhiteSpace(contractId) || contractId.Length > 128 ||
            string.IsNullOrWhiteSpace(dataset) || dataset.Length > 32 || publisherId is null or 0 || instrumentId is null or 0)
            throw new ArgumentException("An exact bounded IFM/provider identity is required.", nameof(contractId));
        if (MessagePackBinarySerializer.MeasureContent(value) > 131072)
            throw new ArgumentException("Reference version is oversized.", nameof(value));

        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
        version = reviewState == ReferenceReviewState.Reviewed ? version : "draft/" + digest;
        if (string.IsNullOrWhiteSpace(version) || version.Length > 128)
            throw new ArgumentException("A bounded mapping version is required.", nameof(version));
        var providerKey = value.Option is { } option
            ? OptionReferenceIdentity.ProviderKey(option)
            : string.Create(CultureInfo.InvariantCulture, $"provider:{dataset}:{publisherId}:{instrumentId}");
        return new(providerKey, identity, new(contractId, version, digest), value);
    }
}
