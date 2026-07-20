using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.MarketData
{
    public enum MarketDirectionType
    {
        Down,
        NeutralDown,
        NeutralUp,
        Up
    }

    public static class MarketDirectionTypeExtensions
    {
        public static string ToStringFast(this MarketDirectionType value) => value switch
        {
            MarketDirectionType.Down => nameof(MarketDirectionType.Down),
            MarketDirectionType.NeutralDown => nameof(MarketDirectionType.NeutralDown),
            MarketDirectionType.NeutralUp => nameof(MarketDirectionType.NeutralUp),
            MarketDirectionType.Up => nameof(MarketDirectionType.Up),
            _ => value.ToString()
        };
    }

}
