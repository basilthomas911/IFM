using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.SystemAdmin;

namespace TomasAI.IFM.Application.Storage.ReferenceDb
{
    public interface IReferenceDbWriteContext : IReferenceDbContext
    {
        Task DeleteLookupTypeAsync(LookupTypeId lookupTypeId);
        Task DeleteScheduledJobAsync(int jobId);
        Task DeleteStrikePriceVolatilityAsync(StrikePriceVolatilityId id);
        Task DeleteEconomicCalendarAsync(EconomicCalendarId id);
        Task InsertLookupTypeAsync(LookupTypeReadModel lookupType);
        Task InsertScheduledJobAsync(ScheduledJobReadModel scheduledJob);
        Task InsertStrikePriceVolatilityAsync(StrikePriceVolatilityReadModel strikePriceVolatility);
        Task InsertEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar);
        Task InsertEconomicCalendarsAsync(ICollection<EconomicCalendarReadModel> economicCalendars);
        Task UpdateScheduledJobAsync(ScheduledJobReadModel scheduledJob);
        Task UpdateStrikePriceVolatilityAsync(StrikePriceVolatilityId id, StrikePriceVolatilityReadModel strikePriceVolatility);
        Task UpdateEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel e);
        Task UpdateLookupTypeAsync(LookupTypeId id, LookupTypeReadModel e);
        new Task BackupDatabaseAsync(DatabaseBackupType backupType, int commandTimeout, Action<string> onInfoMessage);

    }
}
