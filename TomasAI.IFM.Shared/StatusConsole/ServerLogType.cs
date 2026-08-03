using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.StatusConsole;

public enum ServerLogType
{
    Command = 0,
    Query = 1,
    Event = 2,
    Nats = 4,
    Telemetry = 5
}

public static class ServerLogTypeExtensions
{
    public static string ToStringFast(this ServerLogType value) => value switch
    {
        ServerLogType.Command => nameof(ServerLogType.Command),
        ServerLogType.Query => nameof(ServerLogType.Query),
        ServerLogType.Event => nameof(ServerLogType.Event),
        ServerLogType.Nats => nameof(ServerLogType.Nats),
        ServerLogType.Telemetry => nameof(ServerLogType.Telemetry),
        _ => value.ToString()
    };
}
