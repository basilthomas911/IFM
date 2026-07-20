using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Command.Client;

public class ReferenceCommandApi(ICommandService commandSvc) : IReferenceCommandApi
{
    const string ReferenceController = "Reference";
    readonly ICommandService _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// add economic calendar
    /// </summary>
    /// <param name="economicCalendar"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
        => await new AddEconomicCalendarCommand( economicCalendar)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ReferenceController));

    /// <summary>
    /// change economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="economicCalendar"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, bool overwrite)
        => await new ChangeEconomicCalendarCommand( economicCalendarId, economicCalendar) { Overwrite = overwrite }
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ReferenceController));

    /// <summary>
    /// remove economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId economicCalendarId, bool overwrite)
        => await new RemoveEconomicCalendarCommand(economicCalendarId) { Overwrite = overwrite }
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ReferenceController));

    /// <summary>
    /// import economic calendars
    /// </summary>
    /// <param name="importedDate"></param>
    /// <param name="economicCalendars"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync( DateTime importedDate, EconomicCalendarReadModel[] economicCalendars)
        => await new ImportEconomicCalendarsCommand( economicCalendars, importedDate)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ReferenceController));

    /// <summary>
    /// add economic calendar
    /// </summary>
    /// <param name="lookupType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddLookupTypeAsync(LookupTypeReadModel lookupType)
        => await new AddLookupTypeCommand(lookupType)
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ReferenceController));

    /// <summary>
    /// change lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="lookupType"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite)
        => await new ChangeLookupTypeCommand(lookupTypeId, lookupType) { Overwrite = overwrite }
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ReferenceController));

    /// <summary>
    /// remove lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveLookupTypeAsync(LookupTypeId lookupTypeId, bool overwrite)
        => await new RemoveLookupTypeCommand(lookupTypeId) { Overwrite = overwrite }
            .ExecuteAsync(e => _commandSvc.ExecuteApiCommandAsync(e, ReferenceController));
}
