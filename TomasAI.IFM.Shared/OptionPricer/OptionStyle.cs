using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public enum OptionStyle
    {
        American,
        European
    }

    public static class OptionStyleExtensions
    {
        public static string ToStringFast(this OptionStyle value) => value switch
        {
            OptionStyle.American => nameof(OptionStyle.American),
            OptionStyle.European => nameof(OptionStyle.European),
            _ => value.ToString()
        };
    }
}
