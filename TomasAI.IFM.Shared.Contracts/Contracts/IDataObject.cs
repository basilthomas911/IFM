using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Contracts
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3246", Justification = "The invariant generic argument identifies the exact actor, state, result or handler contract in dependency registration and dispatch.")]
    public interface IDataObject<T>
    {
        T ToDTO();
    }
}
