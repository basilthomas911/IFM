using System.Security.Cryptography;
using System.Buffers;
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
        hash.AppendData(snapshot.SourceSequence == 0 ? Prefix : "broker-account-snapshot-v2"u8);
        Span<byte> environmentBytes = stackalloc byte[1] { (byte)environment };
        hash.AppendData(environmentBytes);
        if(snapshot.SourceSequence != 0) hash.AppendData(MessagePackSerializer.Serialize(snapshot));
        else
        {
            // Preserve the exact v1 nine-field MessagePack identity for saved/legacy evidence.
            var bytes=new ArrayBufferWriter<byte>();var writer=new MessagePackWriter(bytes);writer.WriteArrayHeader(9);
            MessagePackSerializer.Serialize(ref writer,snapshot.AccountAlias);MessagePackSerializer.Serialize(ref writer,snapshot.Currency);
            MessagePackSerializer.Serialize(ref writer,snapshot.CashBalance);MessagePackSerializer.Serialize(ref writer,snapshot.AvailableFunds);
            MessagePackSerializer.Serialize(ref writer,snapshot.Complete);MessagePackSerializer.Serialize(ref writer,snapshot.NewRiskAllowed);
            MessagePackSerializer.Serialize(ref writer,snapshot.Generation);MessagePackSerializer.Serialize(ref writer,snapshot.AsOfUtc);
            MessagePackSerializer.Serialize(ref writer,snapshot.Positions);writer.Flush();hash.AppendData(bytes.WrittenSpan);
        }

        Span<byte> digest = stackalloc byte[32];
        if (!hash.TryGetHashAndReset(digest, out var written) || written != digest.Length)
            throw new CryptographicException("Unable to create the broker-account snapshot identity.");
        return new Guid(digest[..16]);
    }
}
