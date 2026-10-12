using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal;

internal sealed record FuturesTradeSignalPrerequisiteResult(
    FuturesTradeSignalInputs? Inputs,
    string? MissingInputs);
