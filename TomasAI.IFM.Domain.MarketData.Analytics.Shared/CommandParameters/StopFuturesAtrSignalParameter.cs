using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;
public record StopFuturesAtrSignalParameter(FuturesAtrSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
