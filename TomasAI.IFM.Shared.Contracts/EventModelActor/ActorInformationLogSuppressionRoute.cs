using System.Collections.Frozen;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Identifies one exact actor type, actor name, and message verb whose routine Information logs are suppressed.</summary>
public sealed class ActorInformationLogSuppressionRoute
{
    public ActorType ActorType { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Verb { get; set; } = string.Empty;
}
