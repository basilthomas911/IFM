using System.Security.Cryptography;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Model;

internal static class TradeHandoffIdentity
{
    public static Guid Create(params string[] components)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', components)));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
