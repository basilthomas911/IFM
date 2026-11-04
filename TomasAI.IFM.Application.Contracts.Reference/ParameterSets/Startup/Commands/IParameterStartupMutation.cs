using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
public interface IParameterStartupMutation : ICommand<ParameterStartupEntityId>
{ Guid RunId { get; } string ExpectedFingerprint { get; } bool PostEvents { get; } }

