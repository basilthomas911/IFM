using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

/// <summary>Pins an active parent Portfolio to the source and business versions used in Fund preparation.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FundActivationPreparation
{
    [Key(0)] public long ParentSourceRevision { get; init; }
    [Key(1)] public long ParentPortfolioVersion { get; init; }
}
