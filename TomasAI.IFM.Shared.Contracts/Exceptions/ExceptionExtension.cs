using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Exceptions
{
    public static class ExceptionExtension
    {

        extension(Exception ex)
        {
            public Exception UnWrapException()
            {
                ArgumentNullException.ThrowIfNull(ex);
                while (ex.InnerException != null)
                    ex = ex.InnerException;
                return ex;
            }
        }
    }
}
