using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Reflection;
using System.Data.Common;


namespace TomasAI.IFM.Framework.Storage.Postgres
{
    public class PostgresObjectDataReader<TResult> : ObjectDataReader<TResult>
    {
        DbDataReader _dataReader;

        /// <summary>
        /// create postgres object data reader
        /// </summary>
        /// <param name="dataReader">db data reader</param>
        /// <param name="resultTypeMap"></param>
        public PostgresObjectDataReader(DbDataReader dataReader,  Dictionary<Type, object> resultTypeMap = null)
            :base(dataReader, resultTypeMap)
        {
            _dataReader = dataReader;
        }

        public override Task<bool> ReadAsync() => _dataReader.ReadAsync();

    }

}
