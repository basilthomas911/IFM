namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>Identifies the reviewed Native Databento seed recipe and exact provider registration.</summary>
public static class DatabentoIndicatorSeedSourceVersion
{
    /// <summary>Builds the durable cache version used by the Host and the API startup reader.</summary>
    public static string Create(DatabentoContractRegistration registration, string defaultDataset)
        => Create(registration.ProviderContractName, registration.Dataset ?? defaultDataset);

    /// <summary>Builds the coverage version from a qualified provider symbol and dataset.</summary>
    public static string Create(string providerContractName, string dataset)
    {
        // Bump the recipe version when seed qualification or provider decoding changes.
        var recipe = dataset == "XCBF.PITCH" ? "native-databento-trade-seed-v2" : "native-databento-seed-v1";
        return $"{recipe}|{dataset.Length}:{dataset}|{providerContractName}";
    }
}
