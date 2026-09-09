using System.Globalization;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Culture-independent persisted scope identifiers; display names never form capacity keys.</summary>
public static class FinancialScopeKeys
{
    public static string Portfolio(int id)=>id.ToString(CultureInfo.InvariantCulture);
    public static string Fund(int id)=>id.ToString(CultureInfo.InvariantCulture);
    public static string Deployment(CatalogKey key)=>$"{(int)key.Kind}:{key.Id:N}:{key.Version.ToString(CultureInfo.InvariantCulture)}";
    /// <summary>One product bucket across contract expiries and workflow horizons; exchange/currency remain explicit.</summary>
    public static string Underlying(string symbol,string exchange,string currency)
    {
        static string Part(string value)
        {
            if(string.IsNullOrWhiteSpace(value) || value.Trim().Length>64) throw new ArgumentException("A bounded product symbol, exchange and currency are required.");
            return Uri.EscapeDataString(value.Trim().ToUpperInvariant());
        }
        var key=$"U1:{Part(symbol)}|{Part(exchange)}|{Part(currency)}";
        if(key.Length>128) throw new ArgumentException("Normalized product scope exceeds 128 characters.");
        return key;
    }
}
