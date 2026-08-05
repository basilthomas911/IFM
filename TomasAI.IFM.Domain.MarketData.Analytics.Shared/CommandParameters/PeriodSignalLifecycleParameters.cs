using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.CommandParameters;

public record StartFuturesMacdSignalParameter(FuturesMacdSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
public record StopFuturesMacdSignalParameter(FuturesMacdSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
public record StartFuturesAdxSignalParameter(FuturesAdxSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
public record StopFuturesAdxSignalParameter(FuturesAdxSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
public record StartFuturesAtrSignalParameter(FuturesAtrSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
public record StopFuturesAtrSignalParameter(FuturesAtrSignalEntityId EntityId, int ErrorCode) : ICommandParameter;
