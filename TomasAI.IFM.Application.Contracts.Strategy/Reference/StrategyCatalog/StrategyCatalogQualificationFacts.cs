using System.Security.Cryptography;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

/// <summary>The exact catalog and policy rows observed together on one database snapshot.</summary>
public sealed record StrategyCatalogQualificationFacts(CatalogKey Root, DateTime AsOfUtc,
    StoredStrategyCatalogDefinition[] Definitions, CatalogPipelinePolicyFact[] Policies)
{
    /// <summary>Computes the fence used to reject a publication if any qualified row changes.</summary>
    public string Fingerprint() => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
}

/// <summary>One persisted immutable policy version and its current publication facts.</summary>
public sealed record CatalogPipelinePolicyFact(CatalogPipelineParameterKind Kind, Guid Id, int Version,
    string PayloadHash, short Status, DateTime? EffectiveFromUtc, string PayloadJson, short SchemaVersion);

/// <summary>Proof of the exact graph qualified by the owning application before the write transaction.</summary>
public sealed record StrategyCatalogPublicationProof(CatalogKey Root, DateTime EffectiveFromUtc, string Fingerprint);
