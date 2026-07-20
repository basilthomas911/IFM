using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using QLNet;

namespace TomasAI.IFM.Shared.EventSourcing;

public record StreamId<TEntityId>(BoundedContextName AggregateName, TEntityId EntityId)
{
    public override string ToString() =>  JsonConvert.SerializeObject(this, Formatting.None, new StringEnumConverter());  
}

public static class StreamId
{
    static Dictionary<string, string> _idMap = [];

    /// <summary>
    /// convert stream id to hash and return as fixed length 64 chars hexidecimal string
    /// </summary>
    /// <returns></returns>
    public static string ToHashCode(string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId))
            throw new ArgumentNullException(nameof(streamId), "StreamId.ToHashCode");
        lock (_idMap)
        {
            if (!_idMap.ContainsKey(streamId))
            {
                var hashCode = new StringBuilder();
                SHA256.HashData(Encoding.UTF8.GetBytes(streamId)).ForEach((i, hashByte) => hashCode.Append(hashByte.ToString("x2")));
                _idMap.Add(streamId, hashCode.ToString());
            }
        }
        return _idMap[streamId];
    }
}

