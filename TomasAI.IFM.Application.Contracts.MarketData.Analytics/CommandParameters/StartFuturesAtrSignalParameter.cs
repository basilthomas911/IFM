using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;
public record StartFuturesAtrSignalParameter(FuturesAtrSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
