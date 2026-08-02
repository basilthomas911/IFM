using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.ServiceApi;

/// <summary>
/// Defines NATS-backed Trade commands intended for use by domain event actors.
/// </summary>
public interface IActorTradeCommandApi
{
    ValueTask<ServiceResult<GuidResult>> ChangeOptionTradeLegDataAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        decimal assetPrice,
        double riskFreeRate,
        OptionTradeLegDataReadModel optionLegData);

    ValueTask<ServiceResult<GuidResult>> UpdateSpreadDistributionStatisticsAsync(
        int orderId,
        int tradeId,
        TradeType tradeType,
        DateOnly valueDate,
        TradeStatus tradeStatus,
        SpreadDistributionReadModel putSpreadDistribution,
        SpreadDistributionReadModel callSpreadDistribution);

    ValueTask<ServiceResult<GuidResult>> ChangeSpreadDistributionStatisticsAsync(
        int orderId,
        int tradeId,
        double forwardLossRatio,
        double lossProbability,
        DateOnly valueDate);
}

public interface IActorTradeCommandApiFactory
{
    IActorTradeCommandApi Create(IEventActorContext context);
}
