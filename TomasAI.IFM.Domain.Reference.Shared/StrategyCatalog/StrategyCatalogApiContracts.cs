using System.Text.Json;
using System.Text.Json.Serialization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

public enum CatalogQueryOperation { List = 1, Exact = 2, DeploymentChoices = 3, ValidatePublishedDeployment = 4 }
public enum CatalogCommandOperation { SaveDraft = 1, Publish = 2, Retire = 3 }
public sealed record CatalogQueryRequest(CatalogQueryOperation Operation, StrategyCatalogKind Kind = StrategyCatalogKind.Family,
    CatalogKey? Key = null, int Limit = 100, string? AfterCode = null);
public sealed record CatalogCommandRequest(Guid OperationId, CatalogCommandOperation Operation,
    StrategyCatalogDefinition? Definition = null, CatalogKey? Key = null, int ExpectedPreviousVersion = 0,
    string? ExpectedHash = null, DateTime? EffectiveUtc = null);
public static class StrategyCatalogJson
{
    static readonly JsonSerializerOptions Options = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32 };
    public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Read<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || System.Text.Encoding.UTF8.GetByteCount(json) > 524288)
            throw new ArgumentException("Catalog message is empty or exceeds the message limit.");
        return JsonSerializer.Deserialize<T>(json, Options) ?? throw new ArgumentException("Catalog message is null.");
    }
}

public sealed record CatalogQueryParameter(CatalogQueryRequest Request) : IQueryParameter
{
    public string? QueryParams => null;
}
public static class StrategyCatalogUris
{
    public const string Query = "/api/reference/strategy-catalog/query";
    public const string Command = "/api/reference/strategy-catalog/command";
}
