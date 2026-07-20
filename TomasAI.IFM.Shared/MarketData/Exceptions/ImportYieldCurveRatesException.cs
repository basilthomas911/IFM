using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.MarketData.Exceptions
{
    public class ImportYieldCurveRatesException : ApplicationException
    {
        public ImportYieldCurveRatesException(string errorMessage) : base(errorMessage)
        {
        }

        public ImportYieldCurveRatesException() : base()
        {
        }

        protected ImportYieldCurveRatesException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
        }

        public ImportYieldCurveRatesException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
