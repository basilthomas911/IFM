using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer.ViewModels
{
    public record SpreadDistributionPathReadModel(
        long Id,
        long SpreadDistributionId,
        int DaysToMaturity,
        double AveragePrice)

    {
    }
}
