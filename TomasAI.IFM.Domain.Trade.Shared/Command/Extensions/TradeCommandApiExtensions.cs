using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Commands;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

/// <summary>
/// Sends Trade-domain mutation commands from a running event actor and returns their typed replies.
/// </summary>
/// <remarks>
/// The implementation constructs option-trade subjects and entity identities before using the captured
/// <see cref="IEventActorContext"/> for request/reply messaging. Create instances through
/// actor context supplied to each extension call.
/// </remarks>
public static class TradeCommandApiExtensions
{

    /// <summary>
    /// Sends the change option trade leg data command and awaits its typed actor reply.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="tradeStatus">The trade lifecycle status.</param>
    /// <param name="assetPrice">The underlying asset price.</param>
    /// <param name="riskFreeRate">The annualized risk-free rate.</param>
    /// <param name="optionLegData">The option-leg data to apply.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> ChangeOptionTradeLegDataAsync(
        this IEventActorContext context,
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
        return RequestAsync<ChangeOptionTradeLegDataCommand, OptionTradeEntityId>(context, command);
    }

    /// <summary>
    /// Sends the update spread distribution statistics command and awaits its typed actor reply.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="tradeType">The trade strategy type.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <param name="tradeStatus">The trade lifecycle status.</param>
    /// <param name="putSpreadDistribution">The put-spread distribution statistics.</param>
    /// <param name="callSpreadDistribution">The call-spread distribution statistics.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> UpdateSpreadDistributionStatisticsAsync(
        this IEventActorContext context,
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
        return RequestAsync<UpdateOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(context, command);
    }

    /// <summary>
    /// Sends the change spread distribution statistics command and awaits its typed actor reply.
    /// </summary>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <param name="forwardLossRatio">The calculated forward-loss ratio.</param>
    /// <param name="lossProbability">The calculated loss probability.</param>
    /// <param name="valueDate">The applicable market value date.</param>
    /// <returns>A value task containing the typed command result returned by the target actor.</returns>
    public static ValueTask<ServiceResult<GuidResult>> ChangeSpreadDistributionStatisticsAsync(
        this IEventActorContext context,
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
        return RequestAsync<ChangeOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(context, command);
    }

    static async ValueTask<ServiceResult<GuidResult>> RequestAsync<TCommand, TEntityId>(IEventActorContext context, TCommand command)
        where TCommand : class, ICommand<TEntityId>
        where TEntityId : IActorEntityId
    {
        var result = await context.RequestAsync<TCommand, TEntityId>(command);
        if (result?.Success != true)
            throw new InvalidOperationException(result?.ErrorMessage);
        return result;
    }
}
