using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.SystemAdmin.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve database names.
/// </summary>
[MessagePackObject(false)]
public record GetDatabaseNamesParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    [SerializationConstructor]
    public GetDatabaseNamesParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => ActorEntityId.Default.Format();
}
