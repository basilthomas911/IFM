using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade.ViewModels;

public record IronCondorSpreadPathDataModel(
    DateOnly valueDate,
    double LossLimit,
    double WinLimit,
    decimal HedgeLimit,
    decimal NetSpread);

