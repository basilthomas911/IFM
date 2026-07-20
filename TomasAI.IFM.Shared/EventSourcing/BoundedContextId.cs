using System.Text;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using QLNet;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.EventSourcing;

public record BoundedContextId<TEntityId>(BoundedContextName AggregateName, TEntityId EntityId)
{
    public override string ToString() =>  JsonConvert.SerializeObject(this, Formatting.None, new StringEnumConverter());  
}

public static class BoundedContextId
{
    static Dictionary<string, string> _idMap = [];

    /// <summary>
    /// Generates a SHA-256 hash code for the specified bounded context identifier.
    /// </summary>
    /// <remarks>This method caches the generated hash codes to improve performance on subsequent calls with
    /// the same identifier.</remarks>
    /// <param name="boundedContextId">The identifier of the bounded context for which to generate the hash code. Cannot be <see langword="null"/>.</param>
    /// <returns>A hexadecimal string representing the SHA-256 hash code of the bounded context identifier.</returns>
    public static string ToHashCode(string boundedContextId)
    {
        IsArgumentNull.Check(boundedContextId, nameof(boundedContextId));
        lock (_idMap)
        {
            if (!_idMap.ContainsKey(boundedContextId))
            {
                var hashCode = new StringBuilder();
                SHA256.HashData(Encoding.UTF8.GetBytes(boundedContextId)).ForEach((i, hashByte) => hashCode.Append(hashByte.ToString("x2")));
                _idMap.Add(boundedContextId, $"{hashCode}");
            }
        }
        return _idMap[boundedContextId];
    }
}

