using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared
{

    public static class OptionTypeExtensions
    {

        extension(OptionType value)
        {
            public string ToStringFast() => value switch
            {
                OptionType.Put => nameof(OptionType.Put),
                OptionType.Call => nameof(OptionType.Call),
                _ => value.ToString()
            };
        }
    }
}
