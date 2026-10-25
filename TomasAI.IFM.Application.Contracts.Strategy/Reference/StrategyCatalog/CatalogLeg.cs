using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public sealed record CatalogLeg(string Key, string InstrumentClass, string Side, string OptionRight,
    decimal Ratio, string ExpiryGroup);
