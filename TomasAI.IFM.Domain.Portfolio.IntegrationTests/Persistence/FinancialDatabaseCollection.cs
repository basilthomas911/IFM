namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

// Schema DDL is process-global; explicit concurrency tests still run independent PostgreSQL connections inside their test.
[CollectionDefinition("PortfolioFinancialDatabase", DisableParallelization=true)]
public sealed class FinancialDatabaseCollection;
