using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{

    public static class StreamingParameterTypeExtensions
    {

        extension(StreamingParameterType value)
        {
            public string ToStringFast() => value switch
            {
                StreamingParameterType.FuturesTickData => nameof(StreamingParameterType.FuturesTickData),
                StreamingParameterType.FuturesOptionTickData => nameof(StreamingParameterType.FuturesOptionTickData),
                _ => value.ToString()
            };
        }
    }
}
