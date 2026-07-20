using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Validation
{
    public interface IValidationRules<TValue> where TValue:class
    {
        ValidationError[] Execute(TValue viewModel); 
    }
}
