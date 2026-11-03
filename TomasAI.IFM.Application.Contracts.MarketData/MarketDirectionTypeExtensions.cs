using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Domain.MarketData.Shared
{

    public static class MarketDirectionTypeExtensions
    {

        extension(MarketDirectionType value)
        {
            public string ToStringFast() => value switch
            {
                MarketDirectionType.Down => nameof(MarketDirectionType.Down),
                MarketDirectionType.NeutralDown => nameof(MarketDirectionType.NeutralDown),
                MarketDirectionType.NeutralUp => nameof(MarketDirectionType.NeutralUp),
                MarketDirectionType.Up => nameof(MarketDirectionType.Up),
                _ => value.ToString()
            };
        }
    }

}
