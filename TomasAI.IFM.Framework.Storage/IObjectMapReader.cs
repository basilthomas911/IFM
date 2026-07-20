using System.Linq.Expressions;

namespace TomasAI.IFM.Framework.Storage;

public interface IObjectMapReader<TResult>
{
    string Get(Expression<Func<TResult, string>> resultPropertyExpr);
    bool Get(Expression<Func<TResult, bool>> resultPropertyExpr);
    bool? Get(Expression<Func<TResult, bool?>> resultPropertyExpr);
    int Get(Expression<Func<TResult, int>> resultPropertyExpr);
    int? Get(Expression<Func<TResult, int?>> resultPropertyExpr);
    short Get(Expression<Func<TResult, short>> resultPropertyExpr);
    short? Get(Expression<Func<TResult, short?>> resultPropertyExpr);
    long Get(Expression<Func<TResult, long>> resultPropertyExpr);
    long? Get(Expression<Func<TResult, long?>> resultPropertyExpr);
    double Get(Expression<Func<TResult, double>> resultPropertyExpr);
    double? Get(Expression<Func<TResult, double?>> resultPropertyExpr);
    float Get(Expression<Func<TResult, float>> resultPropertyExpr);
    float? Get(Expression<Func<TResult, float?>> resultPropertyExpr);
    decimal Get(Expression<Func<TResult, decimal>> resultPropertyExpr);
    decimal? Get(Expression<Func<TResult, decimal?>> resultPropertyExpr);
    DateTime Get(Expression<Func<TResult, DateTime>> resultPropertyExpr);
    DateTime? Get(Expression<Func<TResult, DateTime?>> resultPropertyExpr);
    TimeSpan Get(Expression<Func<TResult, TimeSpan>> resultPropertyExpr);
    TimeSpan? Get(Expression<Func<TResult, TimeSpan?>> resultPropertyExpr);
    DateOnly Get(Expression<Func<TResult, DateOnly>> resultPropertyExpr);
    DateOnly? Get(Expression<Func<TResult, DateOnly?>> resultPropertyExpr);
    TimeOnly Get(Expression<Func<TResult, TimeOnly>> resultPropertyExpr);
    TimeOnly? Get(Expression<Func<TResult, TimeOnly?>> resultPropertyExpr);
    Guid Get(Expression<Func<TResult, Guid>> resultPropertyExpr);
    Guid? Get(Expression<Func<TResult, Guid?>> resultPropertyExpr);
    byte[] Get(Expression<Func<TResult, byte[]>> resultPropertyExpr);
    Enum Get<TEnum>(Expression<Func<TResult, Enum>> resultPropertyExpr) where TEnum : struct, Enum;
    TStruct Get<TStruct>(Expression<Func<TResult, TStruct>> resultPropertyExpr) where TStruct : struct;
    DateTime GetISODateTime(Expression<Func<TResult, DateTime>> resultPropertyExpr);

}
