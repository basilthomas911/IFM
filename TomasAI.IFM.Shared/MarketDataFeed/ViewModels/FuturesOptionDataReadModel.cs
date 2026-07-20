using MessagePack;

namespace TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing futures option pricing and Greek data.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record FuturesOptionDataReadModel
{
    /// <summary>Bid price of the option.</summary>
    [Key(0)]
    public double BidPrice { get; init; }

    /// <summary>Ask price of the option.</summary>
    [Key(1)]
    public double AskPrice { get; init; }

    /// <summary>Implied volatility of the option.</summary>
    [Key(2)]
    public double ImpliedVolatility { get; init; }

    /// <summary>Delta Greek value.</summary>
    [Key(3)]
    public double Delta { get; init; }

    /// <summary>Gamma Greek value.</summary>
    [Key(4)]
    public double Gamma { get; init; }

    /// <summary>Theta Greek value.</summary>
    [Key(5)]
    public double Theta { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public FuturesOptionDataReadModel() { }

    /// <summary>
    /// MessagePack serialization constructor (Key indices must match property Key attributes).
    /// </summary>
    /// <param name="bidPrice">Bid price (Key 0).</param>
    /// <param name="askPrice">Ask price (Key 1).</param>
    /// <param name="impliedVolatility">Implied volatility (Key 2).</param>
    /// <param name="delta">Delta (Key 3).</param>
    /// <param name="gamma">Gamma (Key 4).</param>
    /// <param name="theta">Theta (Key 5).</param>
    [SerializationConstructor]
    public FuturesOptionDataReadModel(
        double bidPrice,
        double askPrice,
        double impliedVolatility,
        double delta,
        double gamma,
        double theta)
    {
        BidPrice = bidPrice;
        AskPrice = askPrice;
        ImpliedVolatility = impliedVolatility;
        Delta = delta;
        Gamma = gamma;
        Theta = theta;
    }
}
