using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.OptionPricer;

namespace TomasAI.IFM.Domain.OptionPricer.SpreadDistribution.Job.Command.Exceptions;

public class SpreadDistributionJobNotInProgressException : ApplicationException
{
    public SpreadDistributionJobNotInProgressException(SpreadDistributionJobEntityId entityId)
        : base($"A spread distribution job is not in progress for the specified parameters: {entityId.Format()}.")
    {
    }

    public SpreadDistributionJobNotInProgressException(string message)
        : base(message)
    {
    }

    public SpreadDistributionJobNotInProgressException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
