using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

public interface IScyllaDbObjectDataMapper<TResult> : IObjectDataMapper<TResult>
    where TResult : class
{
}
