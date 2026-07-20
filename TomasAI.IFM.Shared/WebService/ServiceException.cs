using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.WebService
{
    public class ServiceException : ApplicationException
    {
        public int ErrorCode { get; set; }

        public ServiceException(int errorCode, string errorMessage)
            :base(errorMessage)
        {
            this.ErrorCode = errorCode;
        }

        public ServiceException(string errorMessage)
            : base(errorMessage)
        {
            this.ErrorCode = -1;
        }

        public ServiceException(Exception innerEx)
            :base("Unknown error", innerEx)
        {
            this.ErrorCode = -1;
        }
    }
}
