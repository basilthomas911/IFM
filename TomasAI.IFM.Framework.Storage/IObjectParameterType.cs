using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectParameterType<TEntity>
    {
        ICollection<IObjectParameterTypeMap<TEntity>> ParameterTypeMaps { get; }
        IObjectParameterType<TEntity> Set(string fieldName, int index = -1);
        IObjectParameterType<TEntity> Set(string fieldName, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, DateTime>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, DateTime>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, DateTime?>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, DateTime?>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, TimeSpan>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, TimeSpan>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, string>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, string>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, int>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, int>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, int?>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, int?>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, long>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, long>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, double>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, double>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, double?>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, double?>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, decimal?>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, decimal?>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, decimal>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, decimal>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, bool>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, bool>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, Enum>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, Enum>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, Guid>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, Guid>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, byte[]>> fieldNameExpr, int index = -1);
        IObjectParameterType<TEntity> Set(Expression<Func<TEntity, byte[]>> fieldNameExpr, Func<IObject<TEntity>, object> asTypeOf, int index = -1);
    }
}
