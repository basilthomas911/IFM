using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TomasAI.IFM.Shared.StatusConsole;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1027", Justification = "Historical log categories are individual persisted enum values, not bit flags; preserve all numeric identities.")]
public enum ServerLogType
{
    Command = 0,
    Query = 1,
    Event = 2,
    Nats = 4,
    Telemetry = 5
}
