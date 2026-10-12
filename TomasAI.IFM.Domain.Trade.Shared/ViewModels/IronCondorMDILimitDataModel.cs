using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.ViewModels;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S101", Justification = "Preserve this existing persisted CLR type identity and acronym spelling.")]
public record IronCondorMDILimitDataModel(
    OptionTradeEntityId Id,
    DateOnly ValueDate,
    double Value,
    double WarningLimit,
    double MaxLimit)
{
}
