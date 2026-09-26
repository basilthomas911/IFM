using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public interface IParameterSetFact : IEvent<ParameterSetEntityId>
{
    long Revision {get;} string RequestHash {get;} string VersionJson {get;} string AuditJson {get;}
}
