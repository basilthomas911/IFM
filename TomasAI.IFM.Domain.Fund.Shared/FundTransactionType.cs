namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Specifies the types of transactions that can occur within a fund, such as trades, commissions, profit and loss
/// adjustments, deposits, and withdrawals.
/// </summary>
/// <remarks>Use this enumeration to categorize and track different financial activities within a fund. The values
/// distinguish between standard transactions, adjustments, and operational events, enabling accurate reporting and
/// reconciliation of fund activity.</remarks>
public enum FundTransactionType
{
    Unknown = 0,
    OpeningTrade,
    TradeCommission,
    UnrealizedTradePnl,
    RealizedTradePnl,
    OpeningTradeAdjustment,
    TradeCommissionAdjustment,
    UnrealizedTradePnlAdjustment,
    RealizedTradePnlAdjustment,
    EndOfDayProcessed,
    CashDeposit, 
    CashDepositAdjustment,
    CashWithdrawal,
    CashWithdrawalAdjustment
}

public static class FundTransactionTypeExtensions
{
    public static string ToStringFast(this FundTransactionType value) => value switch
    {
        FundTransactionType.Unknown => nameof(FundTransactionType.Unknown),
        FundTransactionType.OpeningTrade => nameof(FundTransactionType.OpeningTrade),
        FundTransactionType.TradeCommission => nameof(FundTransactionType.TradeCommission),
        FundTransactionType.UnrealizedTradePnl => nameof(FundTransactionType.UnrealizedTradePnl),
        FundTransactionType.RealizedTradePnl => nameof(FundTransactionType.RealizedTradePnl),
        FundTransactionType.OpeningTradeAdjustment => nameof(FundTransactionType.OpeningTradeAdjustment),
        FundTransactionType.TradeCommissionAdjustment => nameof(FundTransactionType.TradeCommissionAdjustment),
        FundTransactionType.UnrealizedTradePnlAdjustment => nameof(FundTransactionType.UnrealizedTradePnlAdjustment),
        FundTransactionType.RealizedTradePnlAdjustment => nameof(FundTransactionType.RealizedTradePnlAdjustment),
        FundTransactionType.EndOfDayProcessed => nameof(FundTransactionType.EndOfDayProcessed),
        FundTransactionType.CashDeposit => nameof(FundTransactionType.CashDeposit),
        FundTransactionType.CashDepositAdjustment => nameof(FundTransactionType.CashDepositAdjustment),
        FundTransactionType.CashWithdrawal => nameof(FundTransactionType.CashWithdrawal),
        FundTransactionType.CashWithdrawalAdjustment => nameof(FundTransactionType.CashWithdrawalAdjustment),
        _ => value.ToString()
    };
}
