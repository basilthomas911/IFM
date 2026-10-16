using System.Collections.Generic;
using System;
using Serilog.Events;
using Newtonsoft.Json;

namespace TomasAI.IFM.Framework.Telemetry.Logging.Serilog
{
    public class SerilogLogEvent
    {
        [JsonProperty(nameof(Timestamp))]
        public DateTime Timestamp { get; set; }

        [JsonProperty(nameof(Level))]
        public string Level { get; set; } = "Information";

        
        [JsonProperty(nameof(RenderedMessage))]
        public string RenderedMessage { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2227", Justification = "Preserve the existing serialized message/configuration contract, including enum widths, numeric values, and collection shape.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Preserve the published interface/configuration contract used by actor callers, named arguments, and serialized settings.")]
        [JsonProperty(nameof(Properties))]
        public Dictionary<string, string> Properties { get; set; }

        
    }
}