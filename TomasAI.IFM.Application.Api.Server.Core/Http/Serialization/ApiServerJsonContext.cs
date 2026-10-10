using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.Api.Server.Core.Http.Serialization;

/// <summary>
/// Compile-time JSON metadata for API-server-owned hot response paths.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(SupervisorRuntimeSnapshot))]
[JsonSerializable(typeof(LivePipelineHealthSnapshot))]
[JsonSerializable(typeof(MarketDataOperationsHealthReadModel))]
public sealed partial class ApiServerJsonContext : JsonSerializerContext;

internal static class ApiServerJson
{
    internal static void Configure(JsonSerializerOptions options)
    {
        if (!options.TypeInfoResolverChain.Contains(ApiServerJsonContext.Default))
            options.TypeInfoResolverChain.Insert(0, ApiServerJsonContext.Default);
        if (!options.Converters.Any(static converter => converter is JsonStringEnumConverter))
            options.Converters.Add(new JsonStringEnumConverter());
    }
}
