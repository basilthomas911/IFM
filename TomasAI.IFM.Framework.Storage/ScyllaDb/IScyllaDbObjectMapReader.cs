using Cassandra;
using System.Linq.Expressions;

namespace TomasAI.IFM.Framework.Storage.ScyllaDb;

public interface IScyllaDbObjectMapReader<TResult> 
{
    Row Row { get; }

    List<TResult>? Get(Expression<Func<TResult, List<TResult>>> resultPropertyExpr);
    Dictionary<TKey, TResult>? Get<TKey>(Expression<Func<TResult, Dictionary<TKey, TResult>>> resultPropertyExpr) where TKey : notnull;
    HashSet<TResult>? Get(Expression<Func<TResult, HashSet<TResult>>> resultPropertyExpr);
    CqlVector<TResult>? Get(Expression<Func<TResult, CqlVector<TResult>>> resultPropertyExpr);
}
