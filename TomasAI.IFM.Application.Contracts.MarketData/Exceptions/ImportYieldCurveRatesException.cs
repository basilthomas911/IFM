using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.MarketData.Shared.Exceptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing ApplicationException hierarchy observed by caller exception handlers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing ApplicationException hierarchy observed by caller exception handlers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing ApplicationException hierarchy observed by caller exception handlers.")]
    [Serializable]
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
