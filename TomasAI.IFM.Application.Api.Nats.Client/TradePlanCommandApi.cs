using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan;
using TomasAI.IFM.Shared.TradePlan.Commands;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>
/// trade plan command api constructor
/// </summary>
/// <param name="commandService"></param>
public class TradePlanCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), ITradePlanCommandApi
{
    /// <summary>
    /// update trade plan
    /// </summary>
    /// <param name="tradePlan"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateTradePlanAsync(TradePlanReadModel tradePlan)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(tradePlan);
            var entityId = new TradePlanEntityId(tradePlan.OrderId, tradePlan.TradeId, tradePlan.ValueDate);
            var cmd = new UpdateTradePlanCommand(tradePlan)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, UpdateTradePlanCommand.Actor, UpdateTradePlanCommand.Verb, entityId.Format()),
                ErrorCode = UpdateTradePlanCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, UpdateTradePlanCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// update iron condor trade plan
    /// </summary>
    /// <param name="valueDate"></param>
    /// <param name="optionTrades"></param>
    /// <param name="futuresEodData"></param>
    /// <param name="mScore"></param>
    /// <param name="fundBalance"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateIronCondorTradePlanAsync(
       DateOnly valueDate,
       IOptionTradeCollection optionTrades,
       FuturesEodDataV2ReadModel futuresEodData,
       double mScore,
       decimal fundBalance)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(optionTrades);
            var primary = optionTrades.PrimaryTrade;
            var entityId = IronCondorTradePlanId.Create(
                primary.OrderId,
                primary.TradeId,
                primary.TradeType,
                valueDate,
                primary.TradeDate,
                primary.MaturityDate);
            var cmd = new UpdateIronCondorTradePlanCommand(valueDate, optionTrades, futuresEodData, mScore, fundBalance)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, UpdateIronCondorTradePlanCommand.Actor, UpdateIronCondorTradePlanCommand.Verb, entityId.Format()),
                ErrorCode = UpdateIronCondorTradePlanCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, UpdateIronCondorTradePlanCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// update trade plan forward loss limit
    /// </summary>
    /// <param name="forwardLossLimit"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> UpdateTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitReadModel forwardLossLimit)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(forwardLossLimit);
            var cmd = new UpdateTradePlanForwardLossLimitCommand(forwardLossLimit)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, UpdateTradePlanForwardLossLimitCommand.Actor, UpdateTradePlanForwardLossLimitCommand.Verb, forwardLossLimit.EntityId.Format()),
                ErrorCode = UpdateTradePlanForwardLossLimitCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, forwardLossLimit.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, UpdateTradePlanForwardLossLimitCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// clear trade plan forward loss limit
    /// </summary>
    /// <param name="entityId"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ClearTradePlanForwardLossLimitAsync(TradePlanForwardLossLimitEntityId entityId)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            var cmd = new ClearTradePlanForwardLossLimitCommand(entityId)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ClearTradePlanForwardLossLimitCommand.Actor, ClearTradePlanForwardLossLimitCommand.Verb, entityId.Format()),
                ErrorCode = ClearTradePlanForwardLossLimitCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ClearTradePlanForwardLossLimitCommand.ErrorId);
        }
        return serviceResult;
    }
  
}
