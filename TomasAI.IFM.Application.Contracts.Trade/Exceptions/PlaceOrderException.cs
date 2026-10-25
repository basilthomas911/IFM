using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.Exceptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public ApplicationException inheritance contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public ApplicationException inheritance contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public ApplicationException inheritance contract.")]
    public class PlaceOrderException : ApplicationException
    {
        public PlaceOrderException() { }

        public PlaceOrderException(string message, Exception innerException) : base(message, innerException) { }

        public PlaceOrderException(string errorMessage) : base(errorMessage)
        {
        }
    }
}
