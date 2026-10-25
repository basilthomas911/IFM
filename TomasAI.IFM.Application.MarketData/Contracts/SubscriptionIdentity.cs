using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.MarketData.Contracts.Ticker;

namespace TomasAI.IFM.Application.MarketData.Contracts;

public static class SubscriptionIdentity
{
    internal static void Validate(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength
            || !string.Equals(value,value.Trim(),StringComparison.Ordinal) || value.Any(char.IsControl))
            throw new ArgumentException("Subscription identities must be bounded, non-empty and canonical.",nameof(value));
    }

    internal static void Write(BinaryWriter writer, SubscriptionTickerKey key)
    {
        writer.Write(key.ProviderScope);
        writer.Write(key.Dataset);
        writer.Write(key.ContractId);
        writer.Write(key.Schema);
        writer.Write((int)key.AssetKind);
    }

    internal static string Digest(Action<BinaryWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(1);
            write(writer);
        }
        return Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, checked((int)stream.Length))));
    }
}
