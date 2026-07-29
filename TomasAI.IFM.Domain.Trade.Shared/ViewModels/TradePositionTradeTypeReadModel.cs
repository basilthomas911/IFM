using TomasAI.IFM.Shared.Trade;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

public record TradePositionTradeTypeReadModel(
    OptionType OptionType,
    TradeType TradeType
    )
{
}
