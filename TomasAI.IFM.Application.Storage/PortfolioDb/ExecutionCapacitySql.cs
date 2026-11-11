namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Atomic actual-execution capacity settlement and reconstruction under the financial fence.</summary>
public static class ExecutionCapacitySql
{
    public const string Create = Schema.PortfolioSchemaSql.AcceptedExecutionCapacity;
    public const string Read = PortfolioDbSql.ExecutionCapacitySql_Statement_1;
    public const string Write = PortfolioDbSql.ExecutionCapacitySql_Statement_2;
    public const string Source = PortfolioDbSql.ExecutionCapacitySql_Statement_3;
    public const string Approval = PortfolioDbSql.ExecutionCapacitySql_Statement_4;
    public const string Exposures = PortfolioDbSql.ExecutionCapacitySql_Statement_5;
    public const string PostedSources = PortfolioDbSql.ExecutionCapacitySql_Statement_6;
}
