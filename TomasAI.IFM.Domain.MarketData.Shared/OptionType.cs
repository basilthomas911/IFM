using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared
{
    public enum OptionType
    {
        Put,
        Call
    }

    public static class OptionTypeExtensions
    {
        public static string ToStringFast(this OptionType value) => value switch
        {
            OptionType.Put => nameof(OptionType.Put),
            OptionType.Call => nameof(OptionType.Call),
            _ => value.ToString()
        };
    }
}
