using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Events;
using TomasAI.IFM.Domain.MarketData.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

public readonly record struct DatasetWorkerAdmission(
    string Dataset,
    DateOnly ValueDate,
    Guid WorkerInstanceId,
    Guid GenerationId,
    long ManifestRevision);
