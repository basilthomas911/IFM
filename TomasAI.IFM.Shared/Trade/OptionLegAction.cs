using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Trade
{
    public enum OptionLegAction
    {
        Short,
        Long
    }

    public static class OptionLegActionExtensions
    {
        public static string ToStringFast(this OptionLegAction value) => value switch
        {
            OptionLegAction.Short => nameof(OptionLegAction.Short),
            OptionLegAction.Long => nameof(OptionLegAction.Long),
            _ => value.ToString()
        };
    }
}
