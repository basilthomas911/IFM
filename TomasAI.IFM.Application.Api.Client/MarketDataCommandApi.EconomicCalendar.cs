using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.CommandParameters;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Client;

public partial class MarketDataCommandApi
{
    public async Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel calendar)
        => await new AddEconomicCalendarParameter(calendar, AddEconomicCalendarCommand.ErrorId)
            .ExecuteAsync(value => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.AddEconomicCalendar, value));

    public async Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId id, EconomicCalendarReadModel calendar, bool overwrite)
        => await new ChangeEconomicCalendarParameter(id, calendar, overwrite, ChangeEconomicCalendarCommand.ErrorId)
            .ExecuteAsync(value => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.ChangeEconomicCalendar, value));

    public async Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId id, bool overwrite)
        => await new RemoveEconomicCalendarParameter(id, overwrite, RemoveEconomicCalendarCommand.ErrorId)
            .ExecuteAsync(value => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.RemoveEconomicCalendar, value));

    public async Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync(DateTime importedDate, EconomicCalendarReadModel[] calendars)
        => await new ImportEconomicCalendarsParameter(importedDate, calendars, ImportEconomicCalendarsCommand.ErrorId)
            .ExecuteAsync(value => _commandSvc.ExecuteCommandAsync(MarketDataUriPath.ImportEconomicCalendars, value));
}
