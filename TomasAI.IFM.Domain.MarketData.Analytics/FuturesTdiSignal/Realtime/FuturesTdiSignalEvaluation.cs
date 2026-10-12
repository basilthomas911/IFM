using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime;

public sealed record FuturesTdiSignalEvaluation(
    FuturesTdiSignalEntityId EntityId,
    FuturesTdiSignalReadModel Signal,
    FuturesTdiSignalGeneratedEvent Generated);
