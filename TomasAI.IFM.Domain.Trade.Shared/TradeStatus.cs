namespace TomasAI.IFM.Domain.Trade.Shared;

public enum TradeStatus
{
    Open,
    IntraDay,
    EndOfDay,
    Close
}

public static class TradeStatusExtensions
{
    public static string ToStringFast(this TradeStatus value) => value switch
    {
        TradeStatus.Open => nameof(TradeStatus.Open),
        TradeStatus.IntraDay => nameof(TradeStatus.IntraDay),
        TradeStatus.EndOfDay => nameof(TradeStatus.EndOfDay),
        TradeStatus.Close => nameof(TradeStatus.Close),
        _ => value.ToString()
    };
}
