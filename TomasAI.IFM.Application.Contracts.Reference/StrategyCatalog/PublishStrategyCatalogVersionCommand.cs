using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed partial record PublishStrategyCatalogVersionCommand : StrategyCatalogLifecycleCommand
{
    public const string Verb = "Publish";
    [Key(6)] public CatalogKey Key { get; init; } = default!;
    [Key(7)] public string ExpectedHash { get; init; } = string.Empty;
    [Key(8)] public DateTime EffectiveUtc { get; init; }
}
