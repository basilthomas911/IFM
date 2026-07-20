using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TomasAI.IFM.Framework.Storage.Json;

namespace TomasAI.IFM.Framework.Storage.Csv
{
    public class JsonObjectDataReader<TResult> : ObjectDataReader<TResult>
    {
        /// <summary>
        /// create
        /// </summary>
        /// <param name="dataReader">sql server data reader</param>
        /// <param name="resultTypeMap"></param>
        public JsonObjectDataReader(IJsonDataReader dataReader,  Dictionary<Type, object> resultTypeMap = null)
            :base(dataReader, resultTypeMap)
        {
        }

        public override Task<bool> ReadAsync() => throw new NotImplementedException();
    }

}
