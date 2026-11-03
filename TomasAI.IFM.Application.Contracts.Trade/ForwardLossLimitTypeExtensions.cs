using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class ForwardLossLimitTypeExtensions
    {

        extension(ForwardLossLimitType value)
        {
            public string ToStringFast() => value switch
            {
                ForwardLossLimitType.Unknown => nameof(ForwardLossLimitType.Unknown),
                ForwardLossLimitType.LimitWarning => nameof(ForwardLossLimitType.LimitWarning),
                ForwardLossLimitType.LimitReached => nameof(ForwardLossLimitType.LimitReached),
                _ => value.ToString()
            };
        }
    }
}
