using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    public static class ExceptionExtension
    {
        public static Exception UnWrapException(this Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }
    }
}
