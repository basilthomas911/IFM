using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Shared
{
    public enum CurrencyType
    {
        USD,
        CAD
    }

    public static class CurrencyTypeExtensions
    {
        public static string ToStringFast(this CurrencyType value) => value switch
        {
            CurrencyType.USD => nameof(CurrencyType.USD),
            CurrencyType.CAD => nameof(CurrencyType.CAD),
            _ => value.ToString()
        };
    }
}
