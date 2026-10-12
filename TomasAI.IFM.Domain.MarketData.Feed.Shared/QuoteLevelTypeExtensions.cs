using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class QuoteLevelTypeExtensions
    {
        public static string ToStringFast(this QuoteLevelType value) => value switch
        {
            QuoteLevelType.LevelOne => nameof(QuoteLevelType.LevelOne),
            QuoteLevelType.LevelTwo => nameof(QuoteLevelType.LevelTwo),
            _ => value.ToString()
        };
    }
}
