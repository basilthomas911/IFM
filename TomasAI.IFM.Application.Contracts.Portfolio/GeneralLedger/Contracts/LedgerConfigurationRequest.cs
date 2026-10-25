using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Versioned configuration intent. Generated identifiers are supplied by the application service.</summary>
[MessagePackObject]
public sealed record LedgerConfigurationRequest
{
    [Key(0)] public LedgerConfigurationAction Action { get; init; }
    [Key(1)] public int BookId { get; init; }
    [Key(2)] public FinancialBookConfiguration? Book { get; init; }
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(3)] public LedgerAccountDefinition[] Accounts { get; init; } = [];
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
    [Key(4)] public LedgerPostingRule[] Rules { get; init; } = [];
    [Key(5)] public Guid PeriodId { get; init; }
    [Key(6)] public DateOnly PeriodStart { get; init; }
    [Key(7)] public DateOnly PeriodEnd { get; init; }
    [Key(8)] public long ExpectedVersion { get; init; }
    [Key(9)] public string SourceCut { get; init; } = string.Empty;
    [Key(10)] public string Reason { get; init; } = string.Empty;
    [Key(11)] public Guid? ReconciliationId { get; init; }
}
