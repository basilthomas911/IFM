using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

public enum RiskFinancialHandoffPhase { None=0, ReservePending=1, FundPending=2, ConsumePending=3, Consumed=4, Submitted=5, Authorized=6 }

/// <summary>Durable financial handoff checkpoints. Requests and identities are retained verbatim across retries.</summary>
[MessagePackObject]
public sealed record RiskFinancialHandoffState
{
    [Key(0)] public RiskFinancialHandoffPhase Phase { get; init; }
    [Key(1)] public ReservePortfolioTradeRiskCommand ReservationRequest { get; init; } = new();
    [Key(2)] public CapacityReservationCompletedEvent? Reservation { get; init; }
    [Key(3)] public Guid FundCommandId { get; init; }
    [Key(4)] public long FundOrderVersion { get; init; }
    [Key(5)] public FundRiskAuthorizationReference? Authorization { get; init; }
    [Key(6)] public FundRiskAuthorizationEvidence? FundAcceptance { get; init; }
    [Key(7)] public CapacityExecutionAcceptance? ExecutionAcceptance { get; init; }
    [Key(8)] public ConsumeCapacityReservationCommand? ConsumptionRequest { get; init; }
    [Key(9)] public CapacityConsumptionCompletedEvent? Consumption { get; init; }
    [Key(10)] public FinancialExecutionOrder? Order { get; init; }
    [Key(11)] public SubmitEmulatorOrderCommand? SubmissionRequest { get; init; }
    [Key(12)] public EmulatorOrderSubmittedEvent? Submission { get; init; }
}
