using System;
using System.Linq.Expressions;

namespace TomasAI.IFM.Framework.Storage
{
    public class DbModel<TRepo> where TRepo : IObjectRepository
    {
        ObjectDataRepository<TRepo> _repo;

        public DbModel(ObjectDataRepository<TRepo> repo)
        {
            _repo = repo;
        }

        public IObjectTypeMapper<TEntity> Map<TEntity>(Expression<Func<TRepo, DbMap<TEntity>>> typeMapPropertyExpr, string tableName = null)
            => _repo.Map(typeMapPropertyExpr, tableName); 

        public string Schema { get; set; }  
    }
}
