using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

[MessagePackObject]
public sealed partial record SaveStrategyCatalogDraftCommand : StrategyCatalogLifecycleCommand
{
    public const string Verb = "SaveDraft";
    [Key(6)] public string DefinitionJson { get; init; } = string.Empty;
    [Key(7)] public int ExpectedPreviousVersion { get; init; }
}
