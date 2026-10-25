using System.Buffers.Binary;
using TomasAI.IFM.Application.MarketData.Pricing;
using MessagePack;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
using TomasAI.IFM.Application.MarketData.MarketOutlook;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

[MessagePackObject]
public sealed record DatasetPublicationEnvelope
{
    [Key(0)] public required string Dataset { get; init; }
    [Key(1)] public required DateOnly ValueDate { get; init; }
    [Key(2)] public required Guid WorkerInstanceId { get; init; }
    [Key(3)] public required Guid GenerationId { get; init; }
    [Key(4)] public required long ManifestRevision { get; init; }
    [Key(5)] public required long PublicationSequence { get; init; }
    [Key(6)] public required DatasetPublicationKind Kind { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "The published serializer contract requires its existing array shape.")]
    [Key(7)] public required byte[] Payload { get; init; }
}
