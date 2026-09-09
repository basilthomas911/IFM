using System.Collections.Frozen;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

public enum LegacyFinancialDisposition { Quarantined=0, PostedHistory=1, HistoricalOnly=2 }
public enum LedgerImportMode { FullPostedHistory=1, OpeningBalanceWithHistory=2 }
public sealed record LegacyFinancialEvidence(string Currency,bool CashMovementConfirmed,bool CommissionSignConfirmed,
    bool ValuationIsAbsolute,Guid? OriginalSourceId,string SourceReference);
public sealed record LegacyFinancialClassificationResult(FundTransactionType SourceKind,LegacyFinancialDisposition Disposition,
    LedgerTransactionKind? PostingKind,decimal? PostingAmount,string Reason,string SourceHash);

/// <summary>Explicit legacy type/sign classification. Legacy balances and trade-opening snapshots never create capital.</summary>
public static class LegacyFinancialClassification
{
    public static readonly FrozenDictionary<FundTransactionType,string> TypeRules=new Dictionary<FundTransactionType,string>
    {
        [FundTransactionType.Unknown]="LEGACY.TYPE.UNKNOWN",
        [FundTransactionType.OpeningTrade]="LEGACY.OPENING.SNAPSHOT_ONLY",
        [FundTransactionType.TradeCommission]="LEGACY.COMMISSION.CONFIRMED_COST",
        [FundTransactionType.UnrealizedTradePnl]="LEGACY.VALUATION.ABSOLUTE_REQUIRED",
        [FundTransactionType.RealizedTradePnl]="LEGACY.REALIZATION.CLEAR_UNREALIZED",
        [FundTransactionType.OpeningTradeAdjustment]="LEGACY.OPENING.SNAPSHOT_ONLY",
        [FundTransactionType.TradeCommissionAdjustment]="LEGACY.CORRECTION.ORIGINAL_REQUIRED",
        [FundTransactionType.UnrealizedTradePnlAdjustment]="LEGACY.CORRECTION.ORIGINAL_REQUIRED",
        [FundTransactionType.RealizedTradePnlAdjustment]="LEGACY.CORRECTION.ORIGINAL_REQUIRED",
        [FundTransactionType.EndOfDayProcessed]="LEGACY.EOD.MARKER_ONLY",
        [FundTransactionType.CashDeposit]="LEGACY.DEPOSIT.CONFIRMATION_REQUIRED",
        [FundTransactionType.CashDepositAdjustment]="LEGACY.CORRECTION.ORIGINAL_REQUIRED",
        [FundTransactionType.CashWithdrawal]="LEGACY.WITHDRAWAL.CONFIRMATION_REQUIRED",
        [FundTransactionType.CashWithdrawalAdjustment]="LEGACY.CORRECTION.ORIGINAL_REQUIRED"
    }.ToFrozenDictionary();

    public static LegacyFinancialClassificationResult Classify(FundTransactionReadModel source,LegacyFinancialEvidence evidence,LedgerImportMode mode)
    {
        var hash=FinancialCanonicalHash.Compute(source);
        LegacyFinancialClassificationResult Result(LegacyFinancialDisposition disposition,LedgerTransactionKind? kind,decimal? amount,string reason)
            =>new(source.TransactionType,disposition,kind,amount,reason,hash);
        LegacyFinancialClassificationResult Quarantine(string reason)=>Result(LegacyFinancialDisposition.Quarantined,null,null,reason);
        if(!TypeRules.TryGetValue(source.TransactionType,out var rule) || source.TransactionType==FundTransactionType.Unknown)
            return Quarantine("LEGACY.TYPE.UNKNOWN");
        if(source.TransactionId<=0 || source.FundId<0 || source.ValueDate==default || source.TransactionDate==default)
            return Quarantine("LEGACY.IDENTITY_OR_DATE.INVALID");
        if(evidence.Currency!="USD") return Quarantine("LEGACY.CURRENCY.UNQUALIFIED");
        if(string.IsNullOrWhiteSpace(evidence.SourceReference)) return Quarantine("LEGACY.SOURCE.UNQUALIFIED");
        if(mode is not (LedgerImportMode.FullPostedHistory or LedgerImportMode.OpeningBalanceWithHistory)) return Quarantine("LEGACY.MODE.INVALID");
        try { LedgerPostingModel.Money(source.Amount); }
        catch(FinancialOperationException) { return Quarantine("LEGACY.AMOUNT.PRECISION"); }
        if(source.TransactionType is FundTransactionType.OpeningTrade or FundTransactionType.OpeningTradeAdjustment or FundTransactionType.EndOfDayProcessed)
            return Result(LegacyFinancialDisposition.HistoricalOnly,null,null,rule);
        if(mode==LedgerImportMode.OpeningBalanceWithHistory)
            return Result(LegacyFinancialDisposition.HistoricalOnly,null,null,"LEGACY.PRE_CUT.HISTORY_ONLY");
        if(source.TransactionType is FundTransactionType.TradeCommissionAdjustment or FundTransactionType.UnrealizedTradePnlAdjustment or
            FundTransactionType.RealizedTradePnlAdjustment or FundTransactionType.CashDepositAdjustment or FundTransactionType.CashWithdrawalAdjustment)
            // The old DTO has no original/correction relationship. Even supplied lineage still needs a qualified exact journal mapping.
            return Quarantine(evidence.OriginalSourceId is null?rule:"LEGACY.CORRECTION.JOURNAL_MAPPING_REQUIRED");
        if(source.TransactionType is FundTransactionType.CashDeposit or FundTransactionType.CashWithdrawal)
        {
            if(source.Amount<=0 || !evidence.CashMovementConfirmed) return Quarantine(rule);
            return Result(LegacyFinancialDisposition.PostedHistory,
                source.TransactionType==FundTransactionType.CashDeposit?LedgerTransactionKind.DepositConfirmed:LedgerTransactionKind.TradeSettlement,
                source.TransactionType==FundTransactionType.CashDeposit?source.Amount:-source.Amount,
                source.TransactionType==FundTransactionType.CashDeposit?rule:"LEGACY.WITHDRAWAL.HISTORICAL_CASH_OUTFLOW");
        }
        if(source.OrderId<=0 || source.TradeId<=0) return Quarantine("LEGACY.TRADE.LINEAGE_REQUIRED");
        if(source.TransactionType==FundTransactionType.TradeCommission)
            return evidence.CommissionSignConfirmed && source.Amount>0
                ?Result(LegacyFinancialDisposition.PostedHistory,LedgerTransactionKind.Commission,source.Amount,rule):Quarantine(rule);
        if(source.TransactionType==FundTransactionType.UnrealizedTradePnl)
            return evidence.ValuationIsAbsolute?Result(LegacyFinancialDisposition.PostedHistory,LedgerTransactionKind.Valuation,source.Amount,rule):Quarantine(rule);
        if(source.TransactionType==FundTransactionType.RealizedTradePnl)
            return Result(LegacyFinancialDisposition.PostedHistory,LedgerTransactionKind.RealizedPnl,source.Amount,rule);
        return Quarantine("LEGACY.TYPE.UNCLASSIFIED");
    }
}
