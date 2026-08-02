using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Command.Api;

public sealed class ActorTradeCommandApi(IEventActorContext context) : IActorTradeCommandApi
{
    readonly IEventActorContext _context = IsArgumentNull.Set(context);

    public ValueTask<ServiceResult<GuidResult>> ChangeOptionTradeLegDataAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        decimal assetPrice,
        double riskFreeRate,
        OptionTradeLegDataReadModel optionLegData)
    {
        var entityId = new OptionTradeEntityId(orderId, tradeId);
        ChangeOptionTradeLegDataCommand command = new(
            orderId,
            tradeId,
            tradeType,
            valueDate,
            tradeStatus,
            assetPrice,
            riskFreeRate,
            optionLegData)
        {
            Subject = new ActorSubject(
                ActorType.Command,
                ChangeOptionTradeLegDataCommand.Actor,
                ChangeOptionTradeLegDataCommand.Verb,
                entityId.Format()),
            EntityId = entityId
        };
        return RequestAsync<ChangeOptionTradeLegDataCommand, OptionTradeEntityId>(command);
    }

    public ValueTask<ServiceResult<GuidResult>> UpdateSpreadDistributionStatisticsAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution)
    {
        var entityId = new OptionTradeEntityId(orderId, tradeId);
        UpdateOptionTradeSpreadDistributionStatisticsCommand command = new(
            orderId,
            tradeId,
            tradeType,
            tradeStatus,
            valueDate,
            putSpreadDistribution.DaysToExpiry,
            putSpreadDistribution,
            callSpreadDistribution)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                UpdateOptionTradeSpreadDistributionStatisticsCommand.Actor,
                UpdateOptionTradeSpreadDistributionStatisticsCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = UpdateOptionTradeSpreadDistributionStatisticsCommand.ErrorId
        };
        return RequestAsync<UpdateOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(command);
    }

    public ValueTask<ServiceResult<GuidResult>> ChangeSpreadDistributionStatisticsAsync(
        int orderId,
        int tradeId,
        double forwardLossRatio,
        double lossProbability,
        DateOnly valueDate)
    {
        var entityId = new OptionTradeEntityId(orderId, tradeId);
        ChangeOptionTradeSpreadDistributionStatisticsCommand command = new(
            orderId,
            tradeId,
            forwardLossRatio,
            lossProbability,
            valueDate)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(
                ActorType.Command,
                ChangeOptionTradeSpreadDistributionStatisticsCommand.Actor,
                ChangeOptionTradeSpreadDistributionStatisticsCommand.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = ChangeOptionTradeSpreadDistributionStatisticsCommand.ErrorId
        };
        return RequestAsync<ChangeOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(command);
    }

    async ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        var result = await _context.RequestAsync<TCommand, TEntityId>(command);
        if (result?.Success != true)
            throw new InvalidOperationException(result?.ErrorMessage);
        return result;
    }

}

public sealed class ActorTradeCommandApiFactory : IActorTradeCommandApiFactory
{
    public IActorTradeCommandApi Create(IEventActorContext context)
        => new ActorTradeCommandApi(context);
}
