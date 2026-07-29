using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels
{
    public record SpreadDistributionPathValueReadModel(
        long Id,
        long SpreadDistributionId,
        int DaysToMaturity,
        double SpreadValue)
    {
    }
}
