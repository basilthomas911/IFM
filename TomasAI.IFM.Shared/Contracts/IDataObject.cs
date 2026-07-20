using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.Contracts
{
    public interface IDataObject<T>
    {
        T ToDTO();
    }
}
