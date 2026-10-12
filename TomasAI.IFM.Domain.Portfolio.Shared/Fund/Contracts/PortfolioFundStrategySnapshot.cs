using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioFundStrategySnapshot
{
    [Key(0)] public Guid WorkflowId { get; init; }
    [Key(1)] public long WorkflowRevision { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
    [Key(3)] public PortfolioReadModel Portfolio { get; init; } = new();
    [Key(4)] public FundMandateReadModel Fund { get; init; } = new();
    [Key(5)] public FundAllocationReadModel Allocation { get; init; } = new();
    [Key(6)] public FundRiskEnvelopeReadModel RiskEnvelope { get; init; } = new();
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(7)] public FundTradeTemplateAssignmentReadModel[] Assignments { get; init; } = [];
    [Key(8)] public DateTime ResolvedAtUtc { get; init; }
    [Key(9)] public DateTime ValidUntilUtc { get; init; }
    [Key(10)] public string PayloadSha256 { get; init; } = string.Empty;
    [Key(11)] public PortfolioFinancialPolicyReadModel FinancialPolicy { get; init; } = new();

    public PortfolioFundStrategySnapshot DefensiveCopy() => this with
    {
        Portfolio = Portfolio.DefensiveCopy(),
        Fund = Fund.DefensiveCopy(),
        FinancialPolicy = FinancialPolicy.DefensiveCopy(),
        Assignments = [.. Assignments.Select(x => x.DefensiveCopy())],
    };
}
