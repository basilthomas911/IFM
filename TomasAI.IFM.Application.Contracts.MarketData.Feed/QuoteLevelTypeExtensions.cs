using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class QuoteLevelTypeExtensions
    {

        extension(QuoteLevelType value)
        {
            public string ToStringFast() => value switch
            {
                QuoteLevelType.LevelOne => nameof(QuoteLevelType.LevelOne),
                QuoteLevelType.LevelTwo => nameof(QuoteLevelType.LevelTwo),
                _ => value.ToString()
            };
        }
    }
}
