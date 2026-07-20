using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Caching
{
    public class DataCacheServiceException : ApplicationException
    {
        public DataCacheServiceException(string errorMsg) : base(errorMsg)
        {
        }

        public DataCacheServiceException(string errorMsg, Exception ex) : base(errorMsg, ex)
        {
        }
    }
}
