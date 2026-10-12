using System.Collections.Generic;
using System;
using Serilog.Events;
using Newtonsoft.Json;

namespace TomasAI.IFM.Framework.Telemetry.Logging.Serilog
{

    public class LogEvents
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
        public SerilogLogEvent[] Events { get; set; }
    }
}