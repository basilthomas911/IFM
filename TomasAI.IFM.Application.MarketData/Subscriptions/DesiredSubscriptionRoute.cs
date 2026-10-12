using TomasAI.IFM.Application.MarketData.Contracts;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;
public sealed record DesiredSubscriptionRoute(SubscriptionTickerKey Ticker, int EffectiveOwners);
