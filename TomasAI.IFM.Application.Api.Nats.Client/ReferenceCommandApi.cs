using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Application.Api.Nats.Client;

public class ReferenceCommandApi(IActorProducer actorProducer)
    : NatsCommandApi(actorProducer), IReferenceCommandApi
{
    /// <summary>
    /// add economic calendar
    /// </summary>
    /// <param name="economicCalendar"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddEconomicCalendarAsync(EconomicCalendarReadModel economicCalendar)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(economicCalendar);
            var entityId = economicCalendar.Id;
            var cmd = new AddEconomicCalendarCommand(economicCalendar)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format()),
                ErrorCode = AddEconomicCalendarCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddEconomicCalendarCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// change economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="economicCalendar"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeEconomicCalendarAsync(EconomicCalendarId economicCalendarId, EconomicCalendarReadModel economicCalendar, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(economicCalendarId);
            IsArgumentNull.Check(economicCalendar);
            var cmd = new ChangeEconomicCalendarCommand(economicCalendarId, economicCalendar, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeEconomicCalendarCommand.Actor, ChangeEconomicCalendarCommand.Verb, economicCalendarId.Format()),
                ErrorCode = ChangeEconomicCalendarCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeEconomicCalendarCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// remove economic calendar
    /// </summary>
    /// <param name="economicCalendarId"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveEconomicCalendarAsync(EconomicCalendarId economicCalendarId, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(economicCalendarId);
            var cmd = new RemoveEconomicCalendarCommand(economicCalendarId, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveEconomicCalendarCommand.Actor, RemoveEconomicCalendarCommand.Verb, economicCalendarId.Format()),
                ErrorCode = RemoveEconomicCalendarCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveEconomicCalendarCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// import economic calendars
    /// </summary>
    /// <param name="importedDate"></param>
    /// <param name="economicCalendars"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ImportEconomicCalendarsAsync(DateTime importedDate, EconomicCalendarReadModel[] economicCalendars)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(economicCalendars);
            var entityId = new EconomicCalendarId(importedDate, "ZZ", "ImportEconomicCalendars");
            var cmd = new ImportEconomicCalendarsCommand(economicCalendars, importedDate)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ImportEconomicCalendarsCommand.Actor, ImportEconomicCalendarsCommand.Verb, entityId.Format()),
                ErrorCode = ImportEconomicCalendarsCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ImportEconomicCalendarsCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// add lookup type
    /// </summary>
    /// <param name="lookupType"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> AddLookupTypeAsync(LookupTypeReadModel lookupType)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(lookupType);
            var entityId = lookupType.Id;
            var cmd = new AddLookupTypeCommand(lookupType)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, entityId.Format()),
                ErrorCode = AddLookupTypeCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, entityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, AddLookupTypeCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// change lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="lookupType"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> ChangeLookupTypeAsync(LookupTypeId lookupTypeId, LookupTypeReadModel lookupType, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(lookupTypeId);
            IsArgumentNull.Check(lookupType);
            var cmd = new ChangeLookupTypeCommand(lookupTypeId, lookupType, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, ChangeLookupTypeCommand.Actor, ChangeLookupTypeCommand.Verb, lookupTypeId.Format()),
                ErrorCode = ChangeLookupTypeCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, ChangeLookupTypeCommand.ErrorId);
        }
        return serviceResult;
    }

    /// <summary>
    /// remove lookup type
    /// </summary>
    /// <param name="lookupTypeId"></param>
    /// <param name="overwrite"></param>
    /// <returns></returns>
    public async Task<ServiceResult<Guid>> RemoveLookupTypeAsync(LookupTypeId lookupTypeId, bool overwrite)
    {
        Guid cmdId = Guid.NewGuid();
        ServiceResult<Guid> serviceResult;
        try
        {
            IsArgumentNull.Check(lookupTypeId);
            var cmd = new RemoveLookupTypeCommand(lookupTypeId, overwrite)
            {
                CommandId = cmdId,
                Subject = new ActorSubject(ActorType.Command, RemoveLookupTypeCommand.Actor, RemoveLookupTypeCommand.Verb, lookupTypeId.Format()),
                ErrorCode = RemoveLookupTypeCommand.ErrorId
            };
            serviceResult = await SendAsync(cmd, cmd.EntityId);
        }
        catch (Exception ex)
        {
            serviceResult = OnError(ex, cmdId, RemoveLookupTypeCommand.ErrorId);
        }
        return serviceResult;
    }
}
