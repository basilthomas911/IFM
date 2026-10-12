using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
    public class ConcurrencyException : ApplicationException
    {
        public ConcurrencyException() { }



        public ConcurrencyException(string errorMessage) : base(errorMessage)
        {
        }

        public ConcurrencyException(string errorMessage, Exception innerException) : base(errorMessage, innerException)
        {
        }
    }
}
