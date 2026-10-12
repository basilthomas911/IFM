using System;
using System.Collections.Generic;
using System.Text;
using System.Linq.Expressions;

namespace TomasAI.IFM.Shared.Storage
{

    public interface IDbSingleCache
    {
        void Clear();
        int Count();
    }

}
