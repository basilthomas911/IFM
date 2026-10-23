using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace TomasAI.IFM.Domain.Trade.Order.Query.Model;

/// <summary>Manual entry profile, shared by backend preview and preparation. It is not a per-leg premium tick rule.</summary>
public static class ManualOrderEntryPolicy
{
    public static decimal Tick(string symbol, bool futures, decimal? referenceTick, IConfiguration? configuration)
    {
        if (futures) return referenceTick is > 0 ? referenceTick.Value
            : throw new InvalidOperationException("Resolved futures tick metadata is unavailable.");
        var configured = configuration?[$"TradeBroker:ManualEntry:{symbol}:OptionComboTick"];
        if (configured is not null)
            return decimal.TryParse(configured, NumberStyles.Number, CultureInfo.InvariantCulture, out var tick) && tick > 0
                ? tick : throw new InvalidOperationException("The configured option combination tick is invalid.");
        // Preserve the existing ES manual combination entry profile during the backend cutover.
        if (string.Equals(symbol, "ES", StringComparison.OrdinalIgnoreCase)) return .05m;
        throw new InvalidOperationException("A manual option combination tick profile is required for this product.");
    }
}
