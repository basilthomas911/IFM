using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Business purpose determines accounting; signs never implicitly choose a transaction kind.</summary>
public enum LedgerTransactionKind { Undefined = 0, DepositConfirmed = 1, WithdrawalRequested = 2, WithdrawalSettled = 3, WithdrawalCancelled = 4, FundTransfer = 5, TradeSettlement = 6, Commission = 7, RealizedPnl = 8, Valuation = 9, Reversal = 10, Adjustment = 11, OpeningBalance = 12 }
