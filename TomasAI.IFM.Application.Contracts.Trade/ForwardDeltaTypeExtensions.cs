using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class ForwardDeltaTypeExtensions
    {

        extension(ForwardDeltaType value)
        {
            public string ToStringFast() => value switch
            {
                ForwardDeltaType.Unknown => nameof(ForwardDeltaType.Unknown),
                ForwardDeltaType.High => nameof(ForwardDeltaType.High),
                ForwardDeltaType.Normal => nameof(ForwardDeltaType.Normal),
                ForwardDeltaType.Low => nameof(ForwardDeltaType.Low),
                _ => value.ToString()
            };
        }
    }
}
