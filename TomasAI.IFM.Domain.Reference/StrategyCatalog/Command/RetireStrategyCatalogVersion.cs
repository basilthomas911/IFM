using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.StrategyCatalog.Command.Actor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.StrategyCatalog.Command;

public static class RetireStrategyCatalogVersion
{
    extension(RetireStrategyCatalogVersionCommand command)
    {
        public async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(StrategyCatalogService service,
            IEventProjector<StrategyCatalogCommandActor> projector, CancellationToken token)
        {
            var request = new CatalogCommandRequest(command.CommandId, CatalogCommandOperation.Retire, Key: command.Key, ExpectedHash: command.ExpectedHash, EffectiveUtc: command.EffectiveUtc);
            await service.ExecuteEventSourcedAsync(request, command.OriginatedBy, projector, command.EntityId, token).ConfigureAwait(false);
            return new ServiceOk<GuidResult>(new(command.CommandId));
        }
    }
}
