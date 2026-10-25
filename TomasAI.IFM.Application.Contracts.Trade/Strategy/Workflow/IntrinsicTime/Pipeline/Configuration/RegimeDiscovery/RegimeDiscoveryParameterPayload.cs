using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;

/// <summary>Provides the single canonical JSON and SHA-256 representation of Regime Discovery parameters.</summary>
public static class RegimeDiscoveryParameterPayload
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>Serializes the immutable typed parameter set to canonical compact JSON.</summary>
    public static string Serialize(RegimeDiscoveryParameterSet parameterSet)
    {
        ArgumentNullException.ThrowIfNull(parameterSet);
        return JsonSerializer.Serialize(parameterSet, Options);
    }

    /// <summary>Computes the uppercase SHA-256 digest of canonical typed parameter JSON.</summary>
    public static string ComputeSha256(RegimeDiscoveryParameterSet parameterSet)
        => ComputeSha256(Serialize(parameterSet));

    /// <summary>Computes the uppercase SHA-256 digest of an exact UTF-8 JSON payload.</summary>
    public static string ComputeSha256(string payloadJson)
    {
        ArgumentNullException.ThrowIfNull(payloadJson);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));
    }
}
