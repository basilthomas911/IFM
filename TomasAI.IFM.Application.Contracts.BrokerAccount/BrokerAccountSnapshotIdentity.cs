using System.Security.Cryptography;
using MessagePack;
using TomasAI.IFM.Application.TradeBroker.Contracts;

namespace TomasAI.IFM.Domain.BrokerAccount.Contracts;

/// <summary>Creates the stable command identity for one immutable broker-account observation.</summary>
public static class BrokerAccountSnapshotIdentity
{
    private static ReadOnlySpan<byte> Prefix => "broker-account-snapshot-v1"u8;

    /// <summary>
    /// Returns the same identifier for an exact snapshot redelivery and a different identifier when any
    /// command payload field changes.
    /// </summary>
    public static Guid Create(BrokerEnvironment environment, BrokerAccountSnapshotEvidence snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Prefix);
        Span<byte> environmentBytes = stackalloc byte[1] { (byte)environment };
        hash.AppendData(environmentBytes);
        hash.AppendData(MessagePackSerializer.Serialize(snapshot));

        Span<byte> digest = stackalloc byte[32];
        if (!hash.TryGetHashAndReset(digest, out var written) || written != digest.Length)
            throw new CryptographicException("Unable to create the broker-account snapshot identity.");
        return new Guid(digest[..16]);
    }
}
