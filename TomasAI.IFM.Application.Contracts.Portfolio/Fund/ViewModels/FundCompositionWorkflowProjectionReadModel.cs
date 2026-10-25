using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

[MessagePackObject(AllowPrivate = true)]
public sealed record FundCompositionWorkflowProjectionReadModel
{
    [Key(0)] public Guid WorkflowId { get; init; }
    [Key(1)] public int PortfolioId { get; init; }
    [Key(2)] public int FundId { get; init; }
    [Key(3)] public int OrderId { get; init; }
    [Key(4)] public Guid CompositionResultId { get; init; }
    [Key(5)] public string CompositionResultHash { get; init; } = string.Empty;
    [Key(6)] public string Status { get; init; } = string.Empty;
    [Key(7)] public DateTime UpdatedOnUtc { get; init; }
    [Key(8)] public long AggregateVersion { get; init; }
}
