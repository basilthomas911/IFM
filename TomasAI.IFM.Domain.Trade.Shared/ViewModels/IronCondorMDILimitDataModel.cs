using TomasAI.IFM.Shared.Trade;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

public record IronCondorMDILimitDataModel(
    OptionTradeEntityId Id,
    DateOnly ValueDate,
    double Value,
    double WarningLimit,
    double MaxLimit)
{
}
