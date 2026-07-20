using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Framework.Storage
{
    public interface IObjectReader<TResult> : IDisposable 
    {
        bool Read();
        List<TResult> ReadAll();
        List<TResult> ReadAll(Func<IObjectReader<TResult>, TResult> resultTypeMapper);
        List<TResultOut> ReadAll<TResultOut>(Func<TResult, TResultOut> resultMapper);
        TResult ReadSingle();
        TResult ReadSingle(Func<IObjectReader<TResult>, TResult> resultTypeMapper);
        TResultOut ReadSingle<TResultOut>(Func<TResult, TResultOut> resultMapper);
        TScalar ReadScalar<TScalar>(string columnName) where TScalar : struct;
        TScalar ReadScalar<TScalar>() where TScalar : struct;

        Task<bool> ReadAsync();
        Task<List<TResult>> ReadAllAsync();
        Task<List<TResult>> ReadAllAsync(Func<IObjectReader<TResult>, TResult> resultTypeMapper);
        Task<List<TResultOut>> ReadAllAsync<TResultOut>(Func<TResult, TResultOut> resultMapper);
        Task<TResult> ReadSingleAsync();
        Task<TResult> ReadSingleAsync(Func<IObjectReader<TResult>, TResult> resultTypeMapper);
        Task<TResultOut> ReadSingleAsync<TResultOut>(Func<TResult, TResultOut> resultMapper);
        Task<TScalar> ReadScalarAsync<TScalar>(string columnName) where TScalar : struct;
        Task<TScalar> ReadScalarAsync<TScalar>() where TScalar : struct;

        IObject<TResult> Get(string fieldName);
        IObject<TResult> Get(int fieldIndex);
        IObject<TResult> Get<TField>(Expression<Func<TField, string>> fieldNameExpr);

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
        Guid Get(Expression<Func<TResult, Guid>> resultPropertyExpr);
        Guid? Get(Expression<Func<TResult, Guid?>> resultPropertyExpr);
        TEnum Get<TEnum>(Expression<Func<TResult, TEnum>> resultPropertyExpr) where TEnum : struct;
     }
}
