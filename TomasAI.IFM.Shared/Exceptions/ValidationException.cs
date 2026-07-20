using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Exceptions
{
    public class ValidationException : ApplicationException
    {
        private readonly string _validationSource;

        public string ValidationSource => _validationSource;
        public ValidationException(string validationSource, string errorMessage)
            :base(errorMessage)
        {
            _validationSource = validationSource;
        }
    }
}
