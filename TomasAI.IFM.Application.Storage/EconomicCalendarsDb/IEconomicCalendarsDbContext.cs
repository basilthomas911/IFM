using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Application.Storage.EconomicCalendarsDb
{
    public interface IEconomicCalendarsDbContext
    {
        Task<ICollection<EconomicCalendarReadModel>> ReadAsync();
    }
}
