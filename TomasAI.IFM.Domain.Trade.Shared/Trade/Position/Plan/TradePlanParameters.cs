using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

[MessagePackObject]
public sealed record TradePlanParameters
{
    [Key(0)] public int Version { get; init; } = 1;
    [Key(1)] public decimal MaximumLoss { get; init; } = 1_000m;
    [Key(2)] public decimal WarningLoss { get; init; } = 750m;
    [Key(3)] public decimal ProfitTarget { get; init; } = 500m;
    [Key(4)] public decimal MaterialPnlChange { get; init; } = 10m;
    [Key(5)] public decimal MaterialPriceChange { get; init; } = 0.25m;
    [Key(6)] public int MaximumDataAgeSeconds { get; init; } = 30;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0015", Justification = "This parameterless model validation reports malformed persisted instance content, not a caller argument.")]
    public void Validate()
    {
        if (Version < 1 || MaximumLoss <= 0 || WarningLoss <= 0 || WarningLoss > MaximumLoss ||
            ProfitTarget <= 0 || MaterialPnlChange < 0 || MaterialPriceChange < 0 || MaximumDataAgeSeconds < 1)
            throw new ArgumentException("Valid Trade Plan parameters are required.");
    }
}
