using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class OptionLegTypeExtensions
    {
        public static string ToStringFast(this OptionLegType value) => value switch
        {
            OptionLegType.ShortPut => nameof(OptionLegType.ShortPut),
            OptionLegType.LongPut => nameof(OptionLegType.LongPut),
            OptionLegType.ShortCall => nameof(OptionLegType.ShortCall),
            OptionLegType.LongCall => nameof(OptionLegType.LongCall),
            _ => value.ToString()
        };
    }
}
