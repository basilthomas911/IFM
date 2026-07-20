using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;

namespace TomasAI.IFM.Framework.Storage.UnitTests
{
    public class TestObjectDataReader<TResult> : ObjectDataReader<TResult>
    {

        /// <summary>
        /// create
        /// </summary>
        /// <param name="dataReader">sql server data reader</param>
        /// <param name="resultTypeMap"></param>
        public TestObjectDataReader(IDataReader dataReader,  Dictionary<Type, object> resultTypeMap = null)
            :base(dataReader, resultTypeMap)
        {
        }

        public override Task<bool> ReadAsync() => throw new NotImplementedException();

    }

}
