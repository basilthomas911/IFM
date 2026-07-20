using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Telemetry.ViewModels;

namespace TomasAI.IFM.Application.Storage.LogDb
{
    public interface ILogDbContext
    {
        Task InsertEventQueueLogAsync(
            long eventId,
            string eventTypeName,
            EventQueueStatus eventQueueStatus,
            DateTime eventQueueDate,
            string eventFailedMessage=null);

        Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);

        Task InsertCommandLogAsync(CommandLogReadModel commandLog);
        Task InsertQueryLogAsync(QueryLogViewModel queryLog);
        Task InsertEventServiceLogAsync(EventServiceLogViewModel eventServiceLog);
        Task InsertDenormalizerLogAsync(DenormalizerLogViewModel eventServiceLog);
        Task InsertLogEventsAsync(LogEventReadModel[] logEvents);

    }
}
