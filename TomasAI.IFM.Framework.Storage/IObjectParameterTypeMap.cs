using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectParameterTypeMap<TResult>
    {
        string FieldName { get; }
        int Index { get; }
        Func<IObject<TResult>,object> AsTypeOf { get; }
    }
}
