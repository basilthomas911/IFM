using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

/// <summary>Accepted business read-model values; contains no SQL, provider request or workflow action.</summary>
[MessagePackObject]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1819",Justification="Numeric-key arrays are immutable accepted event payloads, matching the existing MessagePack contracts.")]
public sealed record PortfolioProjectionFacts
{
    [Key(0)] public int SchemaVersion { get; init; } = 1;
    [Key(1)] public long Revision { get; init; }
    [Key(2)] public DateTime UpdatedAtUtc { get; init; }
    [Key(3)] public PortfolioReadModel? Portfolio { get; init; }
    [Key(4)] public FundMandateReadModel? Fund { get; init; }
    [Key(5)] public PortfolioFinancialPolicyReadModel[] Policies { get; init; } = [];
    [Key(6)] public FundTradeTemplateAssignmentReadModel? Assignment { get; init; }
    [Key(7)] public FundAllocationReadModel? Allocation { get; init; }
    [Key(8)] public FundRiskEnvelopeReadModel? RiskEnvelope { get; init; }
    [Key(9)] public FundOrderProjectionReadModel? Order { get; init; }
    [Key(10)] public FundOrderTradeProjectionReadModel[] Trades { get; init; } = [];
    [Key(11)] public FundCompositionWorkflowProjectionReadModel? Composition { get; init; }
    [Key(12)] public int[] DeletedTradeIds { get; init; } = [];
    [Key(13)] public int DeletedOrderId { get; init; }
    [Key(14)] public int DeletedPortfolioId { get; init; }
    [Key(15)] public int DeletedPolicyId { get; init; }
    [Key(17)] public long OrderRevision { get; init; }
    [Key(16)] public int DeletedPolicyPortfolioId { get; init; }
}
