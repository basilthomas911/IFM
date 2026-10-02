using NATS.Client.Core;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.EventModelActor;

/// <summary>Carries supervised worker identity outside the immutable business-event wire payload.</summary>
public static class RealtimeSourceGenerationHeaders
{
    public const string DatasetKey = "ifm-source-dataset";
    public const string GenerationKey = "ifm-source-generation";

    public static NatsHeaders? Add(NatsHeaders? headers, object message)
    {
        if (message is not IRealtimeSourceGeneration source || source.SourceGenerationId == Guid.Empty)
            return headers;
        if (string.IsNullOrWhiteSpace(source.SourceDataset) || source.SourceDataset.Length > 64)
            throw new InvalidDataException("Supervised realtime source dataset is invalid.");
        headers ??= new NatsHeaders();
        headers[DatasetKey] = source.SourceDataset;
        headers[GenerationKey] = source.SourceGenerationId.ToString("N");
        return headers;
    }

    public static bool TryRead(NatsHeaders? headers, out string dataset, out Guid generation)
    {
        dataset = string.Empty;
        generation = Guid.Empty;
        if (headers is null || !headers.TryGetValue(DatasetKey, out var datasetValue)
            || !headers.TryGetValue(GenerationKey, out var generationValue)) return false;
        dataset = datasetValue.ToString();
        return dataset.Length is > 0 and <= 64
            && Guid.TryParseExact(generationValue.ToString(), "N", out generation)
            && generation != Guid.Empty;
    }
}
