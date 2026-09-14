using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies a concrete strategy-position stream for an established trade.</summary>
[MessagePackObject]
public readonly record struct StrategyPositionId(
    [property: Key(0)] TradeEntityId Trade,
    [property: Key(1)] Guid PositionId) : IActorEntityId
{
    /// <summary>Creates the stable position-stream identity assigned when an established trade is handed to its strategy position actor.</summary>
    /// <param name="trade">The globally unique Portfolio, Fund, Order, and Trade identity.</param>
    /// <param name="strategyKind">The strategy that owns the position stream.</param>
    /// <returns>The deterministic position identity used by command, query, and Trade Plan messages.</returns>
    /// <exception cref="ArgumentException">Thrown when the trade identity or strategy is unsupported.</exception>
    public static StrategyPositionId Create(TradeEntityId trade, TradeStrategyKind strategyKind)
    {
        if (!trade.IsValid)
            throw new ArgumentException("A valid Trade identity is required.", nameof(trade));

        var purpose = strategyKind switch
        {
            TradeStrategyKind.IronCondor or TradeStrategyKind.VerticalSpread => "strategy-position",
            TradeStrategyKind.FuturesOutright => "futures-position",
            _ => throw new ArgumentException($"Strategy {strategyKind} does not own a supported position stream.", nameof(strategyKind))
        };
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}|{trade.Format()}"));
        return new StrategyPositionId(trade, new Guid(bytes.AsSpan(0, 16)));
    }

    /// <summary>Formats the complete strategy-position identity for actor routing.</summary>
    /// <returns>The invariant Portfolio, Fund, Order, Trade, and position identity.</returns>
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Trade.Format()}.{PositionId:N}");
    [IgnoreMember] public bool IsValid => Trade.IsValid && PositionId != Guid.Empty;
}
