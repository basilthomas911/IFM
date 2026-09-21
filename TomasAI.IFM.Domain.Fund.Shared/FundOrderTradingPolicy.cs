using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>Temporary two-trade policy for one opening and one closing strategy trade.</summary>
public static class FundOrderTradingPolicy
{
    public const int MaximumTradeCount = 2;

    public static bool HasEconomicEvidence(FundOrderTradeReadModel trade)
        => trade.TradeState switch
        {
            TradeState.NewTrade => false,
            TradeState.OrderCancelled => trade.HasFillEvidence is not false,
            _ => true
        };

    public static bool CanRemoveTrade(FundOrderReadModel order, FundOrderTradeReadModel trade)
        => order.OrderStatus == OrderStatus.Open
            && order.Trades.Length < MaximumTradeCount
            && !HasEconomicEvidence(trade)
            && order.Trades.All(candidate =>
                candidate.TradeState is not (TradeState.TradeToClose or TradeState.OrderCompleted));

    public static bool CanDeleteOrder(FundOrderReadModel order)
        => order.OrderStatus == OrderStatus.Open
            && order.Trades.All(trade => CanRemoveTrade(order, trade));

    public static bool CanAddTrade(FundOrderReadModel order)
    {
        if (order.OrderStatus != OrderStatus.Open || order.Trades.Length >= MaximumTradeCount)
            return false;
        if (order.Trades.Length == 0)
            return true;
        var opening = order.Trades.SingleOrDefault(trade => trade.PrimaryTrade);
        return opening is not null && opening.TradeState == TradeState.TradeToOpen;
    }

    public static bool CanCloseOrder(FundOrderReadModel order)
        => order.OrderStatus == OrderStatus.Open
            && order.Trades.Length == MaximumTradeCount
            && order.Trades.Count(trade => trade.PrimaryTrade) == 1
            && order.Trades.Any(trade => !trade.PrimaryTrade
                && trade.TradeState == TradeState.OrderCompleted);

    public static bool IsCompatibleAddition(
        FundOrderReadModel order,
        FundOrderTradeReadModel candidate)
    {
        if (!CanAddTrade(order))
            return false;
        if (order.Trades.Length == 0)
            return candidate.PrimaryTrade;
        var opening = order.Trades.Single(trade => trade.PrimaryTrade);
        return !candidate.PrimaryTrade
            && candidate.TradeType == ClosingType(opening.TradeType)
            && string.Equals(candidate.BaseContractSymbol, opening.BaseContractSymbol, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Reference, opening.Reference, StringComparison.Ordinal);
    }

    public static TradeType ClosingType(TradeType openingType) => openingType switch
    {
        TradeType.ShortIronCondor => TradeType.LongIronCondor,
        TradeType.LongIronCondor => TradeType.ShortIronCondor,
        TradeType.PutCreditSpread => TradeType.PutDebitSpread,
        TradeType.PutDebitSpread => TradeType.PutCreditSpread,
        TradeType.CallCreditSpread => TradeType.CallDebitSpread,
        TradeType.CallDebitSpread => TradeType.CallCreditSpread,
        TradeType.FuturesOutright => TradeType.FuturesOutright,
        _ => TradeType.Unknown
    };
}
