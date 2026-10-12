using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class ForwardLossLimitTypeExtensions
    {
        public static string ToStringFast(this ForwardLossLimitType value) => value switch
        {
            ForwardLossLimitType.Unknown => nameof(ForwardLossLimitType.Unknown),
            ForwardLossLimitType.LimitWarning => nameof(ForwardLossLimitType.LimitWarning),
            ForwardLossLimitType.LimitReached => nameof(ForwardLossLimitType.LimitReached),
            _ => value.ToString()
        };
    }
}
