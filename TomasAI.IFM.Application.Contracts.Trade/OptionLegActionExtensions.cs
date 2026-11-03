using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared
{

    public static class OptionLegActionExtensions
    {

        extension(OptionLegAction value)
        {
            public string ToStringFast() => value switch
            {
                OptionLegAction.Short => nameof(OptionLegAction.Short),
                OptionLegAction.Long => nameof(OptionLegAction.Long),
                _ => value.ToString()
            };
        }
    }
}
