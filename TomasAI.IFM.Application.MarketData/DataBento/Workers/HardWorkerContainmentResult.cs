using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Databento.Workers;

public sealed record HardWorkerContainmentResult(
    bool Isolated, IReadOnlyList<int> ProcessIds, Exception? Failure);
