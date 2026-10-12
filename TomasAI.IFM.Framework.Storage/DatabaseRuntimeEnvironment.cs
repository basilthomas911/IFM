using System.Collections.Concurrent;
using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cassandra;
using Npgsql;

namespace TomasAI.IFM.Framework.Storage;
internal enum DatabaseRuntimeEnvironment
{
    Development,
    Test,
    Staging,
    Production
}
