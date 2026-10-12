using MessagePack;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>One fenced Portfolio revision. This is preparation evidence, never a reservation or execution permission.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialAdmissionSnapshot([property: Key(0)] int BookId, [property: Key(1)] int PortfolioId,
    [property: Key(2)] int FundId, [property: Key(3)] string OperatingState, [property: Key(4)] bool MigrationQualified,
    [property: Key(5)] bool CanPrepareAdmission, [property: Key(6)] FinancialAuthorityReference Authority,
    [property: Key(7)] decimal AvailableCash, [property: Key(8)] CapacityLimit[] Limits, [property: Key(9)] CapacityUsed[] Usage,
    [property: Key(10)] string Environment, [property: Key(11)] string ExecutionAccountReference,
    [property: Key(12)] decimal MaximumRiskPerTrade = 0);
