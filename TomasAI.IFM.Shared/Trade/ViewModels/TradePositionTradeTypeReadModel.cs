using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

public record TradePositionTradeTypeReadModel(
    OptionType OptionType,
    TradeType TradeType
    )
{
}
