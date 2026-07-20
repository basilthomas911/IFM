namespace TomasAI.IFM.Framework.Storage;

public interface IObjectPropertyTypeMap<TEntity>
{
    string PropertyName { get; }
    string FieldName { get; }
    Func<IObject<TEntity>, object> AsTypeOf { get; }
}
