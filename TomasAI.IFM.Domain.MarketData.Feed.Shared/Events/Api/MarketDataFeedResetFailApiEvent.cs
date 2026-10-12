using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api;

public record MarketDataFeedResetFailApiEvent : ErrorEvent
{
}
