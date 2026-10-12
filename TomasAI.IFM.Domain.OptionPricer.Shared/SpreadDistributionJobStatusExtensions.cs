using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.OptionPricer.Shared;

public static class SpreadDistributionJobStatusExtensions
{
    public static string ToStringFast(this SpreadDistributionJobStatus value) => value switch
    {
        SpreadDistributionJobStatus.InProgress => nameof(SpreadDistributionJobStatus.InProgress),
        SpreadDistributionJobStatus.Completed => nameof(SpreadDistributionJobStatus.Completed),
        SpreadDistributionJobStatus.Failed => nameof(SpreadDistributionJobStatus.Failed),
        SpreadDistributionJobStatus.Cleared => nameof(SpreadDistributionJobStatus.Cleared),
        _ => value.ToString()
    };
}
