using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

public sealed record DurableSubscriptionSnapshot(
    int SchemaVersion, string Scope, string Dataset, long Revision,
    IReadOnlyList<DurableAuthorityState> Authorities);
