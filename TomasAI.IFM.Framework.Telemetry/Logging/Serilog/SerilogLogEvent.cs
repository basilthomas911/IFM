using System.Collections.Generic;
using System;
using Serilog.Events;
using Newtonsoft.Json;

namespace TomasAI.IFM.Framework.Telemetry.Logging.Serilog
{
    public class SerilogLogEvent
    {
        [JsonProperty("Timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("Level")]
        public string Level { get; set; } = "Information";

        /*
        [JsonProperty("MessageTemplate")]
        public string MessageTemplate { get; set; }
        */
        [JsonProperty("RenderedMessage")]
        public string RenderedMessage { get; set; }

        [JsonProperty("Properties")]
        public Dictionary<string, string> Properties { get; set; }

        /*
        [JsonProperty("Exception")]
        public Exception Exception { get; set; }
        */
    }

    public class LogEvents
    {
        public SerilogLogEvent[] Events { get; set; }
    }
}