namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>Stable command stream identities for both Securities catalogs.</summary>
public static class SecuritiesCatalogStreams
{
    public const string Entity = "catalog";
    public const string Futures = "Command.FuturesContractCommand.catalog";
    public const string Options = "Command.FuturesOptionContractCommand.catalog";
    public const string Migration = "migration/securities/postgres-baseline/v1";
}
