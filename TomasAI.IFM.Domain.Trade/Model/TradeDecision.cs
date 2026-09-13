namespace TomasAI.IFM.Domain.Trade.Model;

/// <summary>Returns an expected domain decision without using exceptions for control flow.</summary>
public readonly record struct TradeDecision<T>(bool Accepted, T? Value, string Code, string Detail)
{
    public static TradeDecision<T> Accept(T value) => new(true, value, string.Empty, string.Empty);
    public static TradeDecision<T> Reject(string code, string detail) => new(false, default, code, detail);
}

