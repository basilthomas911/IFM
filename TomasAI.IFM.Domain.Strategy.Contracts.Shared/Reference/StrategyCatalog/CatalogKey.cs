using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePack.MessagePackObject]
public sealed record CatalogKey([property: MessagePack.Key(0)] StrategyCatalogKind Kind, [property: MessagePack.Key(1)] Guid Id, [property: MessagePack.Key(2)] int Version);
