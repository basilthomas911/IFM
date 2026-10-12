using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;
public record StartFuturesAdxSignalParameter(FuturesAdxSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
