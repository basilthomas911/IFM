namespace TomasAI.IFM.Domain.Trade.Shared;

public static class TradeStatusExtensions
{

    extension(TradeStatus value)
    {
        public string ToStringFast() => value switch
        {
            TradeStatus.Open => nameof(TradeStatus.Open),
            TradeStatus.IntraDay => nameof(TradeStatus.IntraDay),
            TradeStatus.EndOfDay => nameof(TradeStatus.EndOfDay),
            TradeStatus.Close => nameof(TradeStatus.Close),
            _ => value.ToString()
        };
    }
}
