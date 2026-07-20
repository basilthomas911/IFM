using System;
using System.Collections.Generic;
using System.Text;
using TomasAI.IFM.Shared.Trade.ViewModels;

namespace TomasAI.IFM.Shared.Trade
{
    public enum GammaRiskType
    {
        None,
        LowShortPutGamma,
        HighShortPutGamma,
        LowShortCallGamma,
        HighShortCallGamma,
    }

    public static class GammaRiskTypeExtensions
    {
        public static string ToStringFast(this GammaRiskType value) => value switch
        {
            GammaRiskType.None => nameof(GammaRiskType.None),
            GammaRiskType.LowShortPutGamma => nameof(GammaRiskType.LowShortPutGamma),
            GammaRiskType.HighShortPutGamma => nameof(GammaRiskType.HighShortPutGamma),
            GammaRiskType.LowShortCallGamma => nameof(GammaRiskType.LowShortCallGamma),
            GammaRiskType.HighShortCallGamma => nameof(GammaRiskType.HighShortCallGamma),
            _ => value.ToString()
        };
    }
}
