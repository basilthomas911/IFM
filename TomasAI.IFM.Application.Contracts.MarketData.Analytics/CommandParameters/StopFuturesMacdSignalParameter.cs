using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;
public record StopFuturesMacdSignalParameter(FuturesMacdSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
