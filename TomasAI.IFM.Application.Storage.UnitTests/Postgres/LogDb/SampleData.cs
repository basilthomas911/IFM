using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Telemetry.ViewModels;

namespace TomasAI.IFM.Application.Storage.UnitTests.Postgres.LogDb
{
    public static class SampleData
    {
        public static LogEventReadModel LogEvent =>  new (
           DateTime.Now,
           "Information",
           "This is a sample log message.",
           "SampleServiceId"
       );
    }
}
