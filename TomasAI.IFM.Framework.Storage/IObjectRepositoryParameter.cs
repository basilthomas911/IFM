using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Common;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectRepositoryParameter
    {
        DbParameter Parameter { get; }
    }
}
