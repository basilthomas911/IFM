using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;

[MessagePackObject]
public sealed record SelectionConstructionVariant([property: Key(0)] CatalogKey Structure, [property: Key(1)] CatalogKey Variant);
