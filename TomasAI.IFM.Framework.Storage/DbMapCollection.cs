using System;
using System.Collections.Generic;

namespace TomasAI.IFM.Framework.Storage
{
    public class DbMapCollection<TEntity> : List<TEntity> //ICollection<TEntity>
    {
        IObjectRepository _repo;
        DbMap<TEntity> _dbMap;

        /// <summary>
        /// create mapped entity collection
        /// </summary>
        /// <param name="repo"></param>
        /// <param name="dbMap"></param>
        public DbMapCollection(IObjectRepository repo)
        {
            if (repo == null)
                throw new ArgumentException("DbMapCollection: repo parameter is empty");
            _repo = repo;
        }

        /// <summary>
        /// set mapped entity
        /// </summary>
        /// <param name="dbMap"></param>
        public void SetDbMap(DbMap<TEntity> dbMap)
        {
            if (dbMap == null)
                throw new ArgumentException("DbMapCollection.SetDbMap: dbMap parameter is empty");
            _dbMap = dbMap;
        }

    }
}
