using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb.CommandAudit;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

/// <summary>Command state is only routing identity; authority is never cached outside its enlisted transaction.</summary>
public sealed class FinancialCommandState(ActorThreadId id) : IActorState
{
    public ActorThreadId Id { get; set; } = id;
}
