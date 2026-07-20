using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum ForwardDeltaType
    {
        Unknown,
        High,
        Normal,
        Low
    }

    public static class ForwardDeltaTypeExtensions
    {
        public static string ToStringFast(this ForwardDeltaType value) => value switch
        {
            ForwardDeltaType.Unknown => nameof(ForwardDeltaType.Unknown),
            ForwardDeltaType.High => nameof(ForwardDeltaType.High),
            ForwardDeltaType.Normal => nameof(ForwardDeltaType.Normal),
            ForwardDeltaType.Low => nameof(ForwardDeltaType.Low),
            _ => value.ToString()
        };
    }
}
