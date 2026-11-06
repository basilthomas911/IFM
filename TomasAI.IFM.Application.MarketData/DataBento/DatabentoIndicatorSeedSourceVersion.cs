namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>Identifies the reviewed Native Databento seed recipe and exact provider registration.</summary>
public static class DatabentoIndicatorSeedSourceVersion
{
    /// <summary>Builds the durable cache version used by the Host and the API startup reader.</summary>
    public static string Create(DatabentoContractRegistration registration, string defaultDataset)
    {
        var dataset = registration.Dataset ?? defaultDataset;
        // Bump the recipe version when seed qualification or provider decoding changes.
        return $"native-databento-seed-v1|{dataset.Length}:{dataset}|{registration.ProviderContractName}";
    }
}
