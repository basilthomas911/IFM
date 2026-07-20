using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectPropertyType<TEntity>
    {
        ICollection<IObjectPropertyTypeMap<TEntity>> PropertyTypeMaps { get; }
        IObjectPropertyType<TEntity> Set(
            Expression<Func<TEntity, object>> propertyExpr, string fieldName);
        IObjectPropertyType<TEntity> Set(
            Expression<Func<TEntity, Enum>> propertyExpr, Func<IObject<TEntity>, object> asTypeOf, string fieldName);

    }
}
