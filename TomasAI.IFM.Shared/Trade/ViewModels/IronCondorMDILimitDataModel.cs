using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

public record IronCondorMDILimitDataModel(
    OptionTradeEntityId Id,
    DateOnly ValueDate,
    double Value,
    double WarningLimit,
    double MaxLimit)
{
}
