using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.CommandParameters;
using TomasAI.IFM.Shared.Reference.ViewModels;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Api.Client;

public class ReferenceCommandApi(ICommandServiceApi commandSvc)
    : IReferenceCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);

    /// <summary>
    /// add economic calendar
    /// </summary>
    /// <param name="economicCalendar"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
        => await new AddEconomicCalendarParameter(IsArgumentNull.Set(economicCalendar), AddEconomicCalendarCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ReferenceUriPath.AddEconomicCalendar, e));

    /// <summary>
    /// change economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="economicCalendar"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, bool overwrite)
        => await new ChangeEconomicCalendarParameter(IsArgumentNull.Set(economicCalendarId), IsArgumentNull.Set(economicCalendar), overwrite, ChangeEconomicCalendarCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ReferenceUriPath.ChangeEconomicCalendar, e));

    /// <summary>
    /// remove economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId economicCalendarId, bool overwrite)
        => await new RemoveEconomicCalendarParameter(IsArgumentNull.Set(economicCalendarId), overwrite, RemoveEconomicCalendarCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ReferenceUriPath.RemoveEconomicCalendar, e));

    /// <summary>
    /// import economic calendars
    /// </summary>
    /// <param name="importedDate"></param>
    /// <param name="economicCalendars"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync(DateTime importedDate, EconomicCalendarReadModel[] economicCalendars)
        => await new ImportEconomicCalendarsParameter(importedDate, IsArgumentNull.Set(economicCalendars), ImportEconomicCalendarsCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ReferenceUriPath.ImportEconomicCalendars, e));

    /// <summary>
    /// add lookup type
    /// </summary>
    /// <param name="lookupType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddLookupTypeAsync(LookupTypeReadModel lookupType)
        => await new AddLookupTypeParameter(IsArgumentNull.Set(lookupType), AddLookupTypeCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ReferenceUriPath.AddLookupType, e));

    /// <summary>
    /// change lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="lookupType"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite)
        => await new ChangeLookupTypeParameter(IsArgumentNull.Set(lookupTypeId), IsArgumentNull.Set(lookupType), overwrite, ChangeLookupTypeCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ReferenceUriPath.ChangeLookupType, e));

    /// <summary>
    /// remove lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveLookupTypeAsync(LookupTypeId lookupTypeId, bool overwrite)
        => await new RemoveLookupTypeParameter(IsArgumentNull.Set(lookupTypeId), overwrite, RemoveLookupTypeCommand.ErrorId)
            .ExecuteAsync(e => _commandSvc.ExecuteCommandAsync(ReferenceUriPath.RemoveLookupType, e));
}
