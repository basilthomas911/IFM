using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.CommandParameters;
using TomasAI.IFM.Domain.Reference.Shared.Commands;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Application.Shared.Commands;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Framework.Messaging;

namespace TomasAI.IFM.Application.Api.Client;

public class ReferenceCommandApi(ICommandServiceApi commandSvc)
    : IReferenceCommandApi
{
    readonly ICommandServiceApi _commandSvc = IsArgumentNull.Set(commandSvc);
    public Task<ServiceResult<Guid>> ExecuteStrategyCatalogAsync(CatalogCommandRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var command = new StrategyCatalogCommand { CommandId = request.OperationId, RequestJson = StrategyCatalogJson.Write(request),
            Subject = new ActorSubject(ActorType.Command, StrategyCatalogCommand.Actor, StrategyCatalogCommand.Verb, ActorEntityId.Default.Format()) };
        return _commandSvc.PostCommandAsync(StrategyCatalogUris.Command, command).WaitAsync(cancellationToken);
    }


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
