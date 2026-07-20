using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace TomasAI.IFM.Shared.Util
{
    public class BsonSerializer
    {
        private readonly JsonSerializer _serializer;

        public BsonSerializer()
        {
            _serializer = new JsonSerializer();
        }

        /// <summary>
        /// serialize object to bson data
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] Serialize<TData>(TData data)
        {
            var ms = new MemoryStream();
            using (var writer = new BsonDataWriter(ms))
            {
                _serializer.Serialize(writer, data);
            }
            return ms.ToArray();
        }

        /// <summary>
        /// deserialize bson data to object
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="data"></param>
        /// <returns></returns>
        public TData Deserialize<TData>(byte[] data)
        {
            var value = default(TData);
            var ms = new MemoryStream(data);
            using (var reader = new BsonDataReader(ms))
            {
                _serializer.Deserialize<TData>(reader);
            }
            return value;
        }
    }
}
