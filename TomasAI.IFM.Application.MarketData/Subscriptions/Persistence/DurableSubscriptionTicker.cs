using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

/// <summary>Canonical ticker intent only; no prices, provider instrument IDs or native handles.</summary>
public sealed record DurableSubscriptionTicker(
    string ProviderScope, string Dataset, string ContractId, string Schema,
    SubscriptionAssetKind AssetKind, string? UnderlyingContractId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PricingPlanId = null);
