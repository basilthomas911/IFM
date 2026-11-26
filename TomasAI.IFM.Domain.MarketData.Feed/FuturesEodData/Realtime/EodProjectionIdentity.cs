using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime;

internal static class EodProjectionIdentity
{
    internal static Guid For(IEvent source, string effect)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes("eod-projection-v1:" + effect + ":" + EodDeliveryLane.Identity(source))).AsSpan(0, 16));
}
