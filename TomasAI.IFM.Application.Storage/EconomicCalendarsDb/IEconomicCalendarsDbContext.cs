using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Application.Storage.EconomicCalendarsDb
{
    public interface IEconomicCalendarsDbContext
    {
        Task<ICollection<EconomicCalendarReadModel>> ReadAsync();
        Task<ICollection<EconomicCalendarReadModel>> ReadAsync(CancellationToken cancellationToken);
    }
}
