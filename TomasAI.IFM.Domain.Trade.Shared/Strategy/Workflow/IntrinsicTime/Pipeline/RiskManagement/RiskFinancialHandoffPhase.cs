using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

public enum RiskFinancialHandoffPhase { None = 0, ReservePending = 1, FundPending = 2, ConsumePending = 3, Consumed = 4, Submitted = 5, Authorized = 6 }
