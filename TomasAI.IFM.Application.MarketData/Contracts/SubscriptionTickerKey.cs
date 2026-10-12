using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Framework.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

/// <summary>Canonical domain identity. This contains no native instrument ID or provider credential.</summary>
public sealed record SubscriptionTickerKey
{
    public string ProviderScope { get; }
    public string Dataset { get; }
    public string ContractId { get; }
    public string Schema { get; }
    public SubscriptionAssetKind AssetKind { get; }

    public SubscriptionTickerKey(string providerScope, string dataset, string contractId,
        string schema, SubscriptionAssetKind assetKind)
    {
        SubscriptionIdentity.Validate(providerScope, 64);
        SubscriptionIdentity.Validate(dataset, 64);
        SubscriptionIdentity.Validate(contractId, 256);
        SubscriptionIdentity.Validate(schema, 32);
        if (!Enum.IsDefined(assetKind)) throw new ArgumentOutOfRangeException(nameof(assetKind));
        ProviderScope = providerScope;
        Dataset = dataset;
        ContractId = contractId;
        Schema = schema;
        AssetKind = assetKind;
    }
}
