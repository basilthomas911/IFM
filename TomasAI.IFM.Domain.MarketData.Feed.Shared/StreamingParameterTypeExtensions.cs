using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class StreamingParameterTypeExtensions
    {
        public static string ToStringFast(this StreamingParameterType value) => value switch
        {
            StreamingParameterType.FuturesTickData => nameof(StreamingParameterType.FuturesTickData),
            StreamingParameterType.FuturesOptionTickData => nameof(StreamingParameterType.FuturesOptionTickData),
            _ => value.ToString()
        };
    }
}
