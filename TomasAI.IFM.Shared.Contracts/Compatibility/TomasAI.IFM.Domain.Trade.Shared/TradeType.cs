namespace TomasAI.IFM.Domain.Trade.Shared;

public enum TradeType
{
    Unknown,
    ShortPut,
    LongPut,
    ShortCall,
    LongCall,
    PutCreditSpread,
    PutDebitSpread,
    CallCreditSpread,
    CallDebitSpread,
    ShortIronCondor,
    LongIronCondor,
    FuturesOutright
}
