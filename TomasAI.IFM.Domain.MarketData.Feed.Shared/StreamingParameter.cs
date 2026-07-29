using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    public abstract class StreamingParameter
    {
        private StreamingParameterType _stmParamType;

        public StreamingParameterType StreamingParameterType => _stmParamType;

        public StreamingParameter(StreamingParameterType stmParamType)
        {
            _stmParamType = stmParamType;
        }
    }
}
