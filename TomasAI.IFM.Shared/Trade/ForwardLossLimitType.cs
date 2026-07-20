using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum ForwardLossLimitType
    {
        Unknown,
        LimitWarning,
        LimitReached
    }

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
