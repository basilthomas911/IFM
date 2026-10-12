using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public interface IParameterSetMutation : ICommand<ParameterSetEntityId>
{
    string OriginatedBy { get; }
    long ExpectedRevision { get; }
    int Version { get; }
    string ComponentCode { get; }
    string Name { get; }
    string Description { get; }
    int SchemaVersion { get; }
    string PayloadJson { get; }
}
