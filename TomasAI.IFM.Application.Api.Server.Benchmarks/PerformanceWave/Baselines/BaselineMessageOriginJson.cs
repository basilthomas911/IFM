// Frozen before implementation: af6091d39. Benchmark-only baseline.
using System.Text.Json;

using TomasAI.IFM.Shared.EventModelActor;
namespace TomasAI.IFM.Application.Api.Server.Benchmarks.PerformanceWave.Baselines;

/// <summary>Version-one origin encoding shared by durable metadata and transport headers.</summary>
public static class BaselineMessageOriginJson
{
    /// <summary>Encodes known creation metadata; unknown legacy provenance remains SQL/header null.</summary>
    public static string? Serialize(MessageOrigin? origin)
        => origin is null ? null : JsonSerializer.Serialize(origin);

    /// <summary>Decodes validated UTC metadata without consulting a clock.</summary>
    public static MessageOrigin? Deserialize(string? json)
    {
        if (json is null) return null;
        if (json.Length is 0 or > 2048) throw new InvalidDataException("Invalid message origin metadata size.");
        try
        {
            return JsonSerializer.Deserialize<MessageOrigin>(json)
                ?? throw new InvalidDataException("Empty message origin metadata.");
        }
        catch (Exception error) when (error is JsonException or ArgumentException)
        {
            throw new InvalidDataException("Invalid message origin metadata.", error);
        }
    }
}
