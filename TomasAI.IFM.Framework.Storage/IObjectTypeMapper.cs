using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectTypeMapper<TEntity>
    {
        DbMap<TEntity> Properties(Action<IObjectPropertyType<TEntity>> setPropertyTypes);
        DbMap<TEntity> Parameters(Action<IObjectParameterType<TEntity>> setParameterTypes);
    }
}
