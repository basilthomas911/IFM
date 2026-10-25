using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.Validation
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3246", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
    public interface IValidationRules<TValue> where TValue : class
    {
        ValidationError[] Execute(TValue viewModel);
    }
}
