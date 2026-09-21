using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.Pricing;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using System.Security.Cryptography;
using System.Text.Json;

namespace TomasAI.IFM.Application.Storage.SecuritiesDb;

[MessagePackObject]
public sealed record ReferenceContractVersion(
    [property: Key(0)] FuturesContractV3ReadModel? Future,
    [property: Key(1)] FuturesOptionContractReadModel? Option,
    [property: Key(2)] OptionPricingConvention? Convention);

/// <summary>Immutable versions and durable identity reservations. Only committed versions are eligible for pricing.</summary>
public sealed class ReferenceVersionStore(IObjectRepository db)
{
    public const string CreateIdentityTable = """
        CREATE TABLE IF NOT EXISTS securities_reference_identity(identity_key text PRIMARY KEY, binding text);
        """;
    public const string CreateVersionTable = """
        CREATE TABLE IF NOT EXISTS securities_reference_version(contract_id text, version text, digest text, payload blob, published boolean,
            PRIMARY KEY ((contract_id),version));
        """;
    public sealed record Pending(string ContractId, string Version, string Digest);

    public Task<Pending> StageAsync(FuturesContractV3ReadModel value, CancellationToken token = default)
    {
        _ = ReferencePayloadCodec.Write(value);
        var identity = JsonSerializer.Serialize(new { value.ContractId, value.Dataset, value.PublisherId, value.InstrumentId,
            value.Symbol, value.LastTradeDate, value.SecurityType });
        return StageAsync(value.ContractId, value.Dataset, value.PublisherId, value.InstrumentId, value.MappingVersion,
            value.ReviewState, identity, new(value with { OnTheRun = false, Rollover = false }, null, null), token);
    }
    public Task<Pending> StageAsync(FuturesOptionContractReadModel value, CancellationToken token = default)
    {
        _ = ReferencePayloadCodec.Write(value);
        var identity = JsonSerializer.Serialize(new { value.ContractId, value.Dataset, value.PublisherId, value.InstrumentId,
            value.Symbol, value.ContractMonth, value.OptionType, Strike = value.GetExactStrikePrice(), value.UnderlyingContractId,
            value.UnderlyingInstrumentId, value.UnderlyingPublisherId });
        var convention = value.ReviewState == ReferenceReviewState.Reviewed ? ReviewedOptionConvention.From(value) : null;
        return StageAsync(value.ContractId, value.Dataset, value.PublisherId, value.InstrumentId, value.MappingVersion,
            value.ReviewState, identity, new(null, value, convention), token);
    }
    async Task<Pending> StageAsync(string id, string? dataset, ushort? publisher, uint? instrument, string? version,
        ReferenceReviewState review, string identity, ReferenceContractVersion value, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || string.IsNullOrWhiteSpace(dataset) || dataset.Length > 32
            || publisher is null or 0 || instrument is null or 0)
            throw new ArgumentException("An exact bounded IFM/provider identity is required.");
        if (MessagePackBinarySerializer.MeasureContent(value) > 131072) throw new ArgumentException("Reference version is oversized.");
        var digest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
        version = review == ReferenceReviewState.Reviewed ? version : "draft/" + digest;
        if (string.IsNullOrWhiteSpace(version) || version.Length > 128) throw new ArgumentException("A bounded mapping version is required.");
        await ClaimAsync("ifm:" + id, identity, token).ConfigureAwait(false);
        await ClaimAsync(FormattableString.Invariant($"provider:{dataset}:{publisher}:{instrument}"), identity, token).ConfigureAwait(false);
        var payload = MessagePackBinarySerializer.Shared.Serialize(value)!;
        await db.Use("ReferenceVersion.Stage", """
            INSERT INTO securities_reference_version(contract_id,version,digest,payload,published)
            VALUES(:id,:version,:digest,:payload,false) IF NOT EXISTS;
            """).SetParameters(new Values([id, version, digest, payload])).ExecuteCommandAsync(token).ConfigureAwait(false);
        var stored = await ReadAsync(id, version, token).ConfigureAwait(false);
        if (stored is null || stored.Value.Digest != digest)
            throw new InvalidOperationException("Mapping version already contains different content. Publish a new reviewed version.");
        return new(id, version, digest);
    }
    async Task ClaimAsync(string key, string identity, CancellationToken token)
    {
        await db.Use("ReferenceVersion.Claim", "INSERT INTO securities_reference_identity(identity_key,binding) VALUES(:key,:binding) IF NOT EXISTS;")
            .SetParameters(new Values([key, identity])).ExecuteCommandAsync(token).ConfigureAwait(false);
        var bindings = await db.Use("ReferenceVersion.ReadClaim", "SELECT binding FROM securities_reference_identity WHERE identity_key=:key;")
            .SetParameters(new Values([key])).ExecuteQueryAsync(row => row.GetString(0), token).ConfigureAwait(false);
        if (bindings.SingleOrDefault() != identity)
            throw new InvalidOperationException("Provider/IFM identity collision. Existing contract identities cannot be overwritten.");
    }
    public async Task CommitAsync(Pending value, CancellationToken token = default)
    {
        await db.Use("ReferenceVersion.Commit", """
            UPDATE securities_reference_version SET published=true WHERE contract_id=:id AND version=:version IF digest=:digest;
            """).SetParameters(new Values([value.ContractId, value.Version, value.Digest])).ExecuteCommandAsync(token).ConfigureAwait(false);
        var stored = await ReadAsync(value.ContractId, value.Version, token).ConfigureAwait(false);
        if (stored is null || !stored.Value.Published || stored.Value.Digest != value.Digest)
            throw new InvalidOperationException("Reference publication did not complete.");
    }
    public async Task<ReferenceContractVersion?> GetAsync(string id, string version, CancellationToken token = default)
    {
        var row = await ReadAsync(id, version, token).ConfigureAwait(false);
        if (row is null || !row.Value.Published) return null;
        var value = row.Value.Value;
        if ((value.Option?.ContractId ?? value.Future?.ContractId) != id
            || value.Convention is not null && value.Convention != ReviewedOptionConvention.From(value.Option!))
            throw new InvalidDataException("Reference version and pricing convention disagree.");
        return value;
    }
    public async Task<bool> ContainsAsync(string id, string version, CancellationToken token = default) =>
        await ReadAsync(id, version, token).ConfigureAwait(false) is not null;
    /// <summary>Historical pricing must name its accepted version; corrections never silently replace prior evidence.</summary>
    public async Task<ReferenceContractVersion?> GetEffectiveAsync(string id, string version, DateTimeOffset at, CancellationToken token = default)
    {
        if (at.Offset != TimeSpan.Zero) throw new ArgumentException("Historical valuation time must be UTC.");
        var value = await GetAsync(id, version, token).ConfigureAwait(false);
        var from = value?.Option?.EffectiveFromUtc ?? value?.Future?.EffectiveFromUtc;
        var until = value?.Option?.EffectiveUntilUtc ?? value?.Future?.EffectiveUntilUtc;
        return from <= at && until > at ? value : null;
    }
    /// <summary>Bounded keyset history enumeration, including staged revisions with their publication status.</summary>
    public async Task<IReadOnlyList<(string Version, bool Published)>> ListVersionsAsync(string id, string after = "",
        int limit = 100, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || after.Length > 128 || limit is < 1 or > 200)
            throw new ArgumentException("Invalid bounded version-history query.");
        return (await db.Use("ReferenceVersion.History", """
            SELECT version,published FROM securities_reference_version WHERE contract_id=:id AND version>:after LIMIT :page_limit;
            """).SetParameters(new Values([id, after, limit])).ExecuteQueryAsync(
                row => (row.GetString(0), row.GetBool(1)), token).ConfigureAwait(false)).ToArray();
    }
    async Task<(string Digest, bool Published, ReferenceContractVersion Value)?> ReadAsync(string id, string version, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || string.IsNullOrWhiteSpace(version) || version.Length > 128)
            throw new ArgumentException("Exact reference version is required.");
        var rows = (await db.Use("ReferenceVersion.Read", """
            SELECT digest,published,payload FROM securities_reference_version WHERE contract_id=:id AND version=:version;
            """).SetParameters(new Values([id, version])).ExecuteQueryAsync(row => (
                Digest: row.GetString(0), Published: row.GetBool(1),
                Value: Decode(row.GetBytes(2))), token).ConfigureAwait(false)).ToArray();
        if (rows.Length == 0) return null;
        var value = rows.Single();
        if (value.Value is null || Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value.Value))) != value.Digest)
            throw new InvalidDataException("Reference version checksum mismatch.");
        return value;
    }
    static ReferenceContractVersion Decode(byte[] bytes)
    {
        if (bytes.Length is 0 or > 131072) throw new InvalidDataException("Reference version payload exceeds its size bound.");
        return MessagePackBinarySerializer.Shared.Deserialize<ReferenceContractVersion>(bytes)
            ?? throw new InvalidDataException("Reference version payload is missing.");
    }
    readonly record struct Values(object[] Items) : IBindValue { public object Bind() => Items; }
}

/// <summary>Exact imported versions take priority; legacy reviewed mappings remain readable.</summary>
public sealed class SecuritiesOptionPricingConventionStore(IObjectRepository securities, IOptionPricingConventionStore legacy)
    : IOptionPricingConventionStore
{
    public async Task<OptionPricingConvention?> GetAsync(string contractId, string mappingVersion, CancellationToken cancellationToken)
    {
        var store = new ReferenceVersionStore(securities);
        var version = await store.GetAsync(contractId, mappingVersion, cancellationToken).ConfigureAwait(false);
        if (version is not null) return version.Convention;
        if (await store.ContainsAsync(contractId, mappingVersion, cancellationToken).ConfigureAwait(false)) return null;
        return await legacy.GetAsync(contractId, mappingVersion, cancellationToken).ConfigureAwait(false);
    }
}
