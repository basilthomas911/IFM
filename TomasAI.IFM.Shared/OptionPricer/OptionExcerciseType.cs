using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public enum OptionExcerciseType
    {
        European,
        American
    }

    public static class OptionExcerciseTypeExtensions
    {
        public static string ToStringFast(this OptionExcerciseType value) => value switch
        {
            OptionExcerciseType.European => nameof(OptionExcerciseType.European),
            OptionExcerciseType.American => nameof(OptionExcerciseType.American),
            _ => value.ToString()
        };
    }
}
