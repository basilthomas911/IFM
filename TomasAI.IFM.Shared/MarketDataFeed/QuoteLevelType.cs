using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketDataFeed
{
    public enum QuoteLevelType
    {
        LevelOne = 1,
        LevelTwo = 2,
    }

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
