using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
public sealed record CatalogExpiryGroup(string Key, string? AfterGroup = null);
