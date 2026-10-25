using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1012", Justification = "Preserves the existing public construction contract.")]
    public abstract class StreamingParameter
    {
        private readonly StreamingParameterType _stmParamType;

        public StreamingParameterType StreamingParameterType => _stmParamType;

        protected StreamingParameter(StreamingParameterType stmParamType)
        {
            _stmParamType = stmParamType;
        }
    }
}
