using System.Diagnostics;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Measures enlisted PostgreSQL Trade projection work in the caller's current trace.</summary>
public static class TradeStorageTelemetry
{
    public const string SourceName = "TomasAI.IFM.Trade.Persistence";
    public static readonly ActivitySource Source = new(SourceName);
}
