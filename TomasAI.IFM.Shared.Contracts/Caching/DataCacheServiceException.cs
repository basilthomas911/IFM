using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Caching
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0013", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S4052", Justification = "Preserve the existing public exception inheritance contract; CA1058 documents the same compatibility exception.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1058", Justification = "Preserve the existing public exception inheritance contract.")]
    public class DataCacheServiceException : ApplicationException
    {
        public DataCacheServiceException() { }

        public DataCacheServiceException(string errorMsg) : base(errorMsg)
        {
        }

        public DataCacheServiceException(string errorMsg, Exception ex) : base(errorMsg, ex)
        {
        }
    }
}
