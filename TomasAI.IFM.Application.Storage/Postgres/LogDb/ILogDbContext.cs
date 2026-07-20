using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Telemetry.ViewModels;

namespace TomasAI.IFM.Application.Storage.Postgres.LogDb;

public interface ILogDbContext
{
    Task<ICollection<LogEventReadModel>> GetTelemetryLogsByDateRangeAsync(DateTime startDate, DateTime endDate);
    Task InsertTelemetryLogsAsync(LogEventReadModel[] logEvents);
}
