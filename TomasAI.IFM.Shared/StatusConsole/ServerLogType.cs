using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.StatusConsole;

public enum ServerLogType
{
    Command,
    Query,
    Event,
    ZooKeeper,
    Kafka,
    Telemetry
}

public static class ServerLogTypeExtensions
{
    public static string ToStringFast(this ServerLogType value) => value switch
    {
        ServerLogType.Command => nameof(ServerLogType.Command),
        ServerLogType.Query => nameof(ServerLogType.Query),
        ServerLogType.Event => nameof(ServerLogType.Event),
        ServerLogType.ZooKeeper => nameof(ServerLogType.ZooKeeper),
        ServerLogType.Kafka => nameof(ServerLogType.Kafka),
        ServerLogType.Telemetry => nameof(ServerLogType.Telemetry),
        _ => value.ToString()
    };
}
