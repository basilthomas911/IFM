using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Accepted configuration facts; reconstruction never re-evaluates qualification or reconciliation.</summary>
[MessagePackObject]
public sealed record LedgerConfigurationProjectionFacts
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public string ActorName { get; init; } = string.Empty;
    [Key(2)] public LedgerConfigurationRequest Request { get; init; } = new();
    [Key(3)] public FinancialBookConfiguration AcceptedBook { get; init; } = new();
    [Key(4)] public string MigrationState { get; init; } = string.Empty;
    [Key(5)] public LedgerReconciliationResult? Reconciliation { get; init; }
}
