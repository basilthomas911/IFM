using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Shared
{

    public static class CurrencyTypeExtensions
    {

        extension(CurrencyType value)
        {
            public string ToStringFast() => value switch
            {
                CurrencyType.USD => nameof(CurrencyType.USD),
                CurrencyType.CAD => nameof(CurrencyType.CAD),
                _ => value.ToString()
            };
        }
    }
}
