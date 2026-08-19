using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.TradeOrder.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class OptionTradeCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), ITradeCommandApi
{
    public Task<ServiceResult<Guid>> SnapshotAsync(int orderId, int tradeId)
        => SendAsync(
            new OptionTradeEntityId(orderId, tradeId),
            SnapshotOptionTradeCommand.Actor,
            SnapshotOptionTradeCommand.Verb,
            SnapshotOptionTradeCommand.ErrorId,
            (commandId, subject) => new SnapshotOptionTradeCommand(orderId, tradeId)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> DeleteAsync(int orderId, int tradeId)
        => SendAsync(
            new OptionTradeEntityId(orderId, tradeId),
            DeleteOptionTradeCommand.Actor,
            DeleteOptionTradeCommand.Verb,
            DeleteOptionTradeCommand.ErrorId,
            (commandId, subject) => new DeleteOptionTradeCommand(orderId, tradeId)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> PlaceOrderAsync(
        TradeOrderReadModel tradeOrder,
        OptionTradeReadModel optionTrade)
        => SendAsync(
            new OptionTradeEntityId(tradeOrder.OrderId, tradeOrder.TradeId),
            PlaceOptionTradeOrderCommand.Actor,
            PlaceOptionTradeOrderCommand.Verb,
            4007,
            (commandId, subject) => new PlaceOptionTradeOrderCommand(tradeOrder, optionTrade)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> OpenOptionTradeAsync(TradeOrderReadModel tradeOrder)
        => SendAsync(
            new OptionTradeEntityId(tradeOrder.OrderId, tradeOrder.TradeId),
            OpenOptionTradeCommand.Actor,
            OpenOptionTradeCommand.Verb,
            OpenOptionTradeCommand.ErrorId,
            (commandId, subject) => new OpenOptionTradeCommand(tradeOrder)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> CloseOptionTradeAsync(TradeOrderReadModel tradeOrder)
        => SendAsync(
            new OptionTradeEntityId(tradeOrder.OrderId, tradeOrder.TradeId),
            CloseOptionTradeCommand.Actor,
            CloseOptionTradeCommand.Verb,
            4006,
            (commandId, subject) => new CloseOptionTradeCommand(tradeOrder)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> InsertOptionTradeSpreadDataAsync(
        OptionTradeSpreadsDataModel optionTradeSpreadData)
        => SendAsync(
            new OptionTradeEntityId(optionTradeSpreadData.OrderId, optionTradeSpreadData.TradeId),
            InsertOptionTradeSpreadDataCommand.Actor,
            InsertOptionTradeSpreadDataCommand.Verb,
            InsertOptionTradeSpreadDataCommand.ErrorId,
            (commandId, subject) => new InsertOptionTradeSpreadDataCommand(optionTradeSpreadData)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> InsertOptionTradeSpreadBarDataAsync(
        OptionTradeSpreadBarsDataModel optionTradeSpreadBarData)
        => SendAsync(
            new OptionTradeEntityId(optionTradeSpreadBarData.OrderId, optionTradeSpreadBarData.TradeId),
            InsertOptionTradeSpreadBarDataCommand.Actor,
            InsertOptionTradeSpreadBarDataCommand.Verb,
            InsertOptionTradeSpreadBarDataCommand.ErrorId,
            (commandId, subject) => new InsertOptionTradeSpreadBarDataCommand(optionTradeSpreadBarData)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> DeleteOptionTradeSpreadBarDataAsync(
        OptionTradeEntityId optionTradeId,
        TradeType tradeType,
        DateOnly valueDate)
        => SendAsync(
            optionTradeId,
            DeleteOptionTradeSpreadBarDataCommand.Actor,
            DeleteOptionTradeSpreadBarDataCommand.Verb,
            DeleteOptionTradeSpreadBarDataCommand.ErrorId,
            (commandId, subject) => new DeleteOptionTradeSpreadBarDataCommand(optionTradeId, tradeType, valueDate)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> ChangeOptionLegDataAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        decimal assetPrice,
        double riskFreeRate,
        OptionTradeLegDataReadModel optionLegData)
        => SendAsync(
            new OptionTradeEntityId(orderId, tradeId),
            ChangeOptionTradeLegDataCommand.Actor,
            ChangeOptionTradeLegDataCommand.Verb,
            4002,
            (commandId, subject) => new ChangeOptionTradeLegDataCommand(
                orderId,
                tradeId,
                tradeType,
                valueDate,
                tradeStatus,
                assetPrice,
                riskFreeRate,
                optionLegData)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> ChangeDistributionStatisticsAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
        => SendAsync(
            new OptionTradeEntityId(orderId, tradeId),
            UpdateOptionTradeSpreadDistributionStatisticsCommand.Actor,
            UpdateOptionTradeSpreadDistributionStatisticsCommand.Verb,
            UpdateOptionTradeSpreadDistributionStatisticsCommand.ErrorId,
            (commandId, subject) => new UpdateOptionTradeSpreadDistributionStatisticsCommand(
                orderId,
                tradeId,
                tradeType,
                tradeStatus,
                valueDate,
                0,
                putSpreadDistribution,
                callSpreadDistribution)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> ProcessEndOfDayAsync(
        int fundId,
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        decimal openPrice,
        decimal highPrice,
        decimal lowPrice,
        decimal closePrice,
        long volume,
        string reference)
        => SendAsync(
            new OptionTradeEntityId(orderId, tradeId),
            ProcessOptionTradeEndOfDayCommand.Actor,
            ProcessOptionTradeEndOfDayCommand.Verb,
            4008,
            (commandId, subject) => new ProcessOptionTradeEndOfDayCommand(
                fundId,
                orderId,
                tradeId,
                tradeType,
                valueDate,
                tradeStatus,
                openPrice,
                highPrice,
                lowPrice,
                closePrice,
                volume,
                reference)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> UpdateTradeLimitDailyProfitTargetAsync(
        int orderId,
        int tradeId,
        int tradingDays,
        int maxTradingDays)
        => SendAsync(
            new OptionTradeEntityId(orderId, tradeId),
            UpdateOptionTradeDailyProfitTargetCommand.Actor,
            UpdateOptionTradeDailyProfitTargetCommand.Verb,
            UpdateOptionTradeDailyProfitTargetCommand.ErrorId,
            (commandId, subject) => new UpdateOptionTradeDailyProfitTargetCommand(
                orderId,
                tradeId,
                tradingDays,
                maxTradingDays)
            {
                CommandId = commandId,
                Subject = subject
            });

    public Task<ServiceResult<Guid>> DeleteAsync(int orderId)
        => SendAsync(
            new OrderId(orderId),
            DeleteOptionTradesCommand.Actor,
            DeleteOptionTradesCommand.Verb,
            DeleteOptionTradesCommand.ErrorId,
            (commandId, subject) => new DeleteOptionTradesCommand(orderId)
            {
                CommandId = commandId,
                Subject = subject
            });

    async Task<ServiceResult<Guid>> SendAsync<TCommand, TEntityId>(
        TEntityId entityId,
        string actor,
        string verb,
        int errorCode,
        Func<Guid, ActorSubject, TCommand> commandFactory)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        var commandId = Guid.NewGuid();
        try
        {
            var subject = new ActorSubject(ActorType.Command, actor, verb, entityId.Format());
            var command = commandFactory(commandId, subject);
            return await RequestCommandAsync(command, entityId);
        }
        catch (Exception ex)
        {
            return OnError(ex, commandId, errorCode);
        }
    }
}
