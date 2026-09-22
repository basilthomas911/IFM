using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Exact financial approval of an existing composed Fund order. A legacy Approved reference cannot substitute for this contract.</summary>
[MessagePackObject]
public sealed record FundRiskAuthorizationReference
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public Guid RiskInvocationId { get; init; }
    [Key(2)] public Guid RiskResultId { get; init; }
    [Key(3)] public string CompositionResultHash { get; init; } = "";
    [Key(4)] public string UnitCandidateHash { get; init; } = "";
    [Key(5)] public string RiskAssessmentHash { get; init; } = "";
    [Key(6)] public string SizedOrderHash { get; init; } = "";
    [Key(7)] public Guid ReservationId { get; init; }
    [Key(8)] public Guid ReservationCompletedEventId { get; init; }
    [Key(9)] public int StrategyUnits { get; init; }
    [Key(10)] public long FinancialRevision { get; init; }
    [Key(11)] public long AuthorityEpoch { get; init; }
    [Key(12)] public DateTime ValidUntilUtc { get; init; }
    [Key(13)] public string ExecutionEnvironment { get; init; } = "";
    [Key(14)] public int PortfolioId { get; init; }
    [Key(15)] public int FundId { get; init; }
    [Key(16)] public int OrderId { get; init; }
    [Key(17)] public Guid WorkflowId { get; init; }
    [Key(18)] public string RequirementsHash { get; init; } = "";
    [Key(19)] public Guid ReservationOperationId { get; init; }

    public static FundRiskAuthorizationReference From(Guid riskInvocationId, Guid workflowId, CapacityReservationReceipt receipt) => new()
    {
        RiskInvocationId = riskInvocationId, WorkflowId = workflowId, RiskResultId = receipt.RiskResultId,
        CompositionResultHash = receipt.CompositionResultHash, UnitCandidateHash = receipt.UnitCandidateHash,
        RiskAssessmentHash = receipt.RiskAssessmentHash, SizedOrderHash = receipt.SizedOrderHash,
        ReservationId = receipt.ReservationId, ReservationCompletedEventId = receipt.CompletedEventId,
        StrategyUnits = receipt.StrategyUnits, FinancialRevision = receipt.FinancialRevision, AuthorityEpoch = receipt.AuthorityEpoch,
        ValidUntilUtc = receipt.ValidUntilUtc, ExecutionEnvironment = receipt.ExecutionEnvironment,
        PortfolioId = receipt.PortfolioId, FundId = receipt.FundId, OrderId = receipt.OrderId,
        RequirementsHash = receipt.Requirements.ContentHash, ReservationOperationId = receipt.OperationId
    };

    /// <summary>Checks the immutable contract; current reservation and authority checks occur under the database fence.</summary>
    public void Validate()
    {
        if (SchemaVersion != 1 || RiskInvocationId == Guid.Empty || RiskResultId == Guid.Empty ||
            ReservationId == Guid.Empty || ReservationCompletedEventId == Guid.Empty || ReservationOperationId == Guid.Empty ||
            WorkflowId == Guid.Empty || PortfolioId <= 0 || FundId <= 0 || OrderId <= 0 || StrategyUnits <= 0 ||
            FinancialRevision <= 0 || AuthorityEpoch <= 0 || ValidUntilUtc.Kind != DateTimeKind.Utc ||
            ValidUntilUtc == default || ExecutionEnvironment != "Emulator" ||
            new[] { CompositionResultHash, UnitCandidateHash, RiskAssessmentHash, SizedOrderHash, RequirementsHash }
                .Any(x => x is null || x.Length != 64 || !x.All(Uri.IsHexDigit)))
            throw new ArgumentException("Exact versioned Fund financial authorization is required.");
    }
}

/// <summary>Storage validates a new Fund authorization in the same transaction as its Fund event.</summary>
public interface IFundRiskAuthorizedEvent : IEvent
{
    FundRiskAuthorizationReference? FinancialAuthorization { get; }
}

[MessagePackObject]
public sealed record GetFundRiskAuthorizationRequest([property: Key(0)] Guid CommandId);
[MessagePackObject]
public sealed record FundRiskAuthorizationEvidence([property: Key(0)] Guid CommandId, [property: Key(1)] Guid EventId,
    [property: Key(2)] FundRiskAuthorizationReference Authorization);
