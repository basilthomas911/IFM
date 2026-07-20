using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb
{

    public class OptionPricerDbException : Exception
    {
        public OptionPricerDbException(string errorMsg, Exception innerException)
            :base(errorMsg, innerException)
        {
        }
    }

}
