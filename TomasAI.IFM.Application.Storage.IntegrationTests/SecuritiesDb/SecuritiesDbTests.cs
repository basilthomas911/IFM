using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SecuritiesDatabaseNonParallelCollection
{
    public const string Name = "Securities database non-parallel";
}

public class SecuritiesDatabaseFixture : IDisposable
{
    readonly TomasAI.IFM.IntegrationTesting.IsolatedIntegrationInfrastructure infrastructure = new("storage-securities", includeCql: false);

    public SecuritiesDatabaseFixture()
    {
        infrastructure.StartAsync().GetAwaiter().GetResult();
        var dbConn = new DbConnectionSettings()
                         .Add("SecuritiesDbConnection", infrastructure.PostgresConnectionString + ";Search Path=securities,public", "System.Data.Postgres");

        var diContainer = new Dictionary<Type, SecuritiesDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        new TomasAI.IFM.Application.Storage.SecuritiesDb.Schema.SecuritiesSchemaDb(dbConn, logger)
            .CreateAllAsync().GetAwaiter().GetResult();

        var redisCacheMap = new Dictionary<string, string>();


        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        DbFactory = dbFactory;
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), new SecuritiesDbContext(dbConn, DbFactory, logger));
        Db = DbFactory.SecuritiesDb as SecuritiesDbContext;
    }

    public SecuritiesDbContext Db { get; }

    public IDbContextFactory DbFactory { get; }

    public void Dispose() => infrastructure.DisposeAsync().AsTask().GetAwaiter().GetResult();

}

[Collection(SecuritiesDatabaseNonParallelCollection.Name)]
public class SecuritiesDbTests(SecuritiesDatabaseFixture testFixture) : IClassFixture<SecuritiesDatabaseFixture>
{
    readonly SecuritiesDatabaseFixture _testFixture = testFixture;

    readonly record struct ProjectionCompletionState(Guid Generation, bool Completed);

    static async Task<ProjectionCompletionState?> ReadProjectionStateAsync(
        IObjectRepository db,
        string projectionName)
    {
        var states = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionStateV3)}", SecuritiesDbCql.GetSecuritiesProjectionStateV3)
            .SetParameters(new GetSecuritiesProjectionStateV3(projectionName))
            .ExecuteQueryAsync(static row => new ProjectionCompletionState(row.GetGuid(0), row.GetBool(1)));
        return states.Count == 1 ? states.First() : null;
    }

    static async Task<ProjectionCompletionState?> ReadSymbolProjectionStateAsync(
        IObjectRepository db,
        string projectionName,
        string symbol)
    {
        var states = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3)}", SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3)
            .SetParameters(new GetSecuritiesSymbolProjectionStateV3(projectionName, symbol))
            .ExecuteQueryAsync(static row => new ProjectionCompletionState(row.GetGuid(0), row.GetBool(1)));
        return states.Count == 1 ? states.First() : null;
    }

    static async Task DeleteProjectionStateAsync(
        IObjectRepository db,
        string projectionName,
        string? symbol = null)
    {
        if (symbol is not null)
        {
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesSymbolProjectionStateV3)}", SecuritiesDbCql.DeleteSecuritiesSymbolProjectionStateV3)
                .SetParameters(new DeleteSecuritiesSymbolProjectionStateV3(projectionName, symbol))
                .ExecuteCommandAsync();
            return;
        }

        await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionStateV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionStateV3)
            .SetParameters(new DeleteSecuritiesProjectionStateV3(projectionName))
            .ExecuteCommandAsync();
    }

    static async Task ResetProjectionOperationsAsync(IObjectRepository db, string projectionName)
    {
        var operationIds = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)}", SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)
            .SetParameters(new GetSecuritiesProjectionOperationsV3(projectionName))
            .ExecuteQueryAsync(static row => row.GetGuid(0));
        foreach (var operationId in operationIds)
        {
            var scopes = await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.GetSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.GetSecuritiesProjectionOperationScopesV3)
                .SetParameters(new GetSecuritiesProjectionOperationScopesV3(projectionName, operationId))
                .ExecuteQueryAsync(static row => (Type: row.GetString(0), Key: row.GetString(1)));
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)}", SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)
                .SetParameters(new RemoveSecuritiesProjectionOperationV3(operationId, projectionName))
                .ExecuteCommandAsync();
            foreach (var symbol in scopes
                .Where(static scope => scope.Type == "symbol")
                .Select(static scope => scope.Key))
            {
                await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)}", SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(new RemoveSecuritiesSymbolProjectionOperationV3(
                        operationId,
                        projectionName,
                        symbol))
                    .ExecuteCommandAsync();
            }
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)
                .SetParameters(new DeleteSecuritiesProjectionOperationScopesV3(projectionName, operationId))
                .ExecuteCommandAsync();
            await db.Use($"{nameof(SecuritiesDbCql)}.{nameof(SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)}", SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)
                .SetParameters(new DeleteSecuritiesProjectionOperationV3(projectionName, operationId))
                .ExecuteCommandAsync();
        }
    }

    static InsertFuturesContract ToInsertParameters(FuturesContractV3ReadModel contract)
        => new(
            contract.ContractId,
            contract.Description,
            contract.Symbol,
            contract.LocalSymbol,
            contract.SecurityType,
            contract.Currency,
            contract.Exchange,
            contract.Multiplier,
            contract.LastTradeDate,
            contract.OnTheRun,
            contract.Rollover);

    static InsertFuturesOptionContract ToInsertParameters(FuturesOptionContractReadModel contract)
        => new(
            contract.ContractId,
            contract.Description,
            contract.Symbol,
            contract.LocalSymbol,
            contract.SecurityType,
            contract.Currency,
            contract.Exchange,
            contract.Multiplier,
            contract.ContractMonth,
            contract.StrikePrice,
            contract.OptionType);

    [Fact]
    public async Task InsertFuturesContractAsync_ShouldInsertContract()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var futuresContract = SampleData.FuturesContract with
        {
            ContractId = $"INSERT_FUT_{suffix}",
            Symbol = $"INSERT_FUT_SYMBOL_{suffix}",
            LastTradeDate = new DateOnly(2030, 3, 15)
        };

        try
        {
            await _testFixture.Db.InsertFuturesContractAsync(futuresContract);

            var result = await _testFixture.Db.GetFuturesContractAsync(futuresContract.ContractId);
            result.Should().NotBeNull();
            result.ContractId.Should().Be(futuresContract.ContractId);
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesContractAsync(futuresContract.ContractId);
        }
    }

    [Fact]
    public async Task UpdateFuturesContractAsync_ShouldUpdateContract()
    {
        // Arrange
        var futuresContract = SampleData.FuturesContract;
        await _testFixture.Db.DeleteFuturesContractAsync(futuresContract.ContractId);
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract);

        var updatedContract = futuresContract with { Description = "Updated Description" };

        try
        {
            // Act
            await _testFixture.Db.UpdateFuturesContractAsync(futuresContract.Id, updatedContract);

            // Assert
            var result = await _testFixture.Db.GetFuturesContractAsync(updatedContract.ContractId);
            result.Should().NotBeNull();
            result.Description.Should().Be("Updated Description");
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesContractAsync(futuresContract.ContractId);
        }
    }

    [Fact]
    public async Task DeleteFuturesContractAsync_ShouldDeleteContract()
    {
        // Arrange
        var futuresContract = SampleData.FuturesContract;
        await _testFixture.Db.InsertFuturesContractAsync(futuresContract);

        // Act
        await _testFixture.Db.DeleteFuturesContractAsync(futuresContract.ContractId);

        // Assert
        var result = await _testFixture.Db.GetFuturesContractAsync(futuresContract.ContractId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFuturesContractAsync_ShouldReturnContract()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var futuresContract = SampleData.FuturesContract with
        {
            ContractId = $"GET_FUT_{suffix}",
            Symbol = $"GET_FUT_SYMBOL_{suffix}",
            LastTradeDate = new DateOnly(2030, 6, 21)
        };

        try
        {
            await _testFixture.Db.InsertFuturesContractAsync(futuresContract);

            var result = await _testFixture.Db.GetFuturesContractAsync(futuresContract.ContractId);
            result.Should().NotBeNull();
            result.ContractId.Should().Be(futuresContract.ContractId);
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesContractAsync(futuresContract.ContractId);
        }
    }

    [Fact]
    public async Task GetFuturesContractsAsync_ShouldReturnAllContracts()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var symbol = $"GET_ALL_FUT_SYMBOL_{suffix}";
        var futuresContract1 = SampleData.FuturesContract with
        {
            ContractId = $"GET_ALL_FUT_A_{suffix}",
            Symbol = symbol,
            LastTradeDate = new DateOnly(2030, 9, 20),
            Description = "Test Description 1"
        };
        var futuresContract2 = SampleData.FuturesContract with
        {
            ContractId = $"GET_ALL_FUT_B_{suffix}",
            Symbol = symbol,
            LastTradeDate = new DateOnly(2030, 12, 20),
            Description = "Test Description 2"
        };

        try
        {
            await _testFixture.Db.InsertFuturesContractsAsync([futuresContract1, futuresContract2]);

            var result = await _testFixture.Db.GetFuturesContractsAsync();
            result.Should().NotBeNull();
            result.Should().Contain(x => x.ContractId == futuresContract1.ContractId);
            result.Should().Contain(x => x.ContractId == futuresContract2.ContractId);
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesContractAsync(futuresContract1.ContractId);
            await _testFixture.Db.DeleteFuturesContractAsync(futuresContract2.ContractId);
        }
    }

    /*
     write unit tests for the following methods:
        - InsertFuturesOptionContractAsync
        - UpdateFuturesOptionContractAsync
        - DeleteFuturesOptionContractAsync
        - GetFuturesOptionContractAsync
        - GetFuturesOptionContractsAsync
     */

    [Fact]
    public async Task InsertFuturesOptionContractAsync_ShouldInsertContract()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var futuresOptionContract = SampleData.FuturesOptionContract with
        {
            ContractId = $"I{suffix}20300315C1000",
            Symbol = $"INSERT_OPT_SYMBOL_{suffix}",
            ContractMonth = new DateOnly(2030, 3, 15)
        };

        try
        {
            await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

            var result = await _testFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
            result.Should().NotBeNull();
            result.ContractId.Should().Be(futuresOptionContract.ContractId);
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);
        }
    }

    [Fact]
    public async Task UpdateFuturesOptionContractAsync_ShouldUpdateContract()
    {
        // Arrange
        var futuresOptionContract = SampleData.FuturesOptionContract;
        await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);
        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

        var updatedContract = futuresOptionContract with { Description = "Updated Description" };

        try
        {
            // Act
            await _testFixture.Db.UpdateFuturesOptionContractAsync(futuresOptionContract.ContractId, updatedContract);

            // Assert
            var result = await _testFixture.Db.GetFuturesOptionContractAsync(updatedContract.ContractId);
            result.Should().NotBeNull();
            result.Description.Should().Be("Updated Description");
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);
        }
    }

    [Fact]
    public async Task DeleteFuturesOptionContractAsync_ShouldDeleteContract()
    {
        // Arrange
        var futuresOptionContract = SampleData.FuturesOptionContract;
        await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

        // Act
        await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);

        // Assert
        var result = await _testFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetFuturesOptionContractAsync_ShouldReturnContract()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var futuresOptionContract = SampleData.FuturesOptionContract with
        {
            ContractId = $"G{suffix}20300621C1000",
            Symbol = $"GET_OPT_SYMBOL_{suffix}",
            ContractMonth = new DateOnly(2030, 6, 21)
        };

        try
        {
            await _testFixture.Db.InsertFuturesOptionContractAsync(futuresOptionContract);

            var result = await _testFixture.Db.GetFuturesOptionContractAsync(futuresOptionContract.ContractId);
            result.Should().NotBeNull();
            result.ContractId.Should().Be(futuresOptionContract.ContractId);
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract.ContractId);
        }
    }

    [Fact]
    public async Task GetFuturesOptionContractsAsync_ShouldReturnAllContracts()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var symbol = $"GET_ALL_OPT_SYMBOL_{suffix}";
        var futuresOptionContract1 = SampleData.FuturesOptionContract with
        {
            ContractId = $"A{suffix}20300920C1000",
            Symbol = symbol,
            ContractMonth = new DateOnly(2030, 9, 20),
            Description = "Test Description 1"
        };
        var futuresOptionContract2 = SampleData.FuturesOptionContract with
        {
            ContractId = $"B{suffix}20301220C1000",
            Symbol = symbol,
            ContractMonth = new DateOnly(2030, 12, 20),
            Description = "Test Description 2"
        };

        try
        {
            await _testFixture.Db.InsertFuturesOptionContractsAsync(
                [futuresOptionContract1, futuresOptionContract2]);

            var result = await _testFixture.Db.GetFuturesOptionContractsAsync(symbol);
            result.Should().NotBeNull();
            result.Should().Contain(x => x.ContractId == futuresOptionContract1.ContractId);
            result.Should().Contain(x => x.ContractId == futuresOptionContract2.ContractId);
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract1.ContractId);
            await _testFixture.Db.DeleteFuturesOptionContractAsync(futuresOptionContract2.ContractId);
        }
    }

    /// <summary>
    /// Unit test for GetFuturesContractsBySymbolAsync method
    /// </summary>
    [Fact]
    public async Task GetFuturesContractsBySymbolAsync_ReturnsExpectedResults()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var symbol = $"GET_BY_SYMBOL_{suffix}";
        var futuresContract1 = SampleData.FuturesContract with
        {
            ContractId = $"GET_BY_SYMBOL_A_{suffix}",
            Symbol = symbol,
            LastTradeDate = new DateOnly(2031, 3, 21),
            Description = "Test Description 1"
        };
        var futuresContract2 = SampleData.FuturesContract with
        {
            ContractId = $"GET_BY_SYMBOL_B_{suffix}",
            Symbol = symbol,
            LastTradeDate = new DateOnly(2031, 6, 20),
            Description = "Test Description 2"
        };

        try
        {
            await _testFixture.Db.InsertFuturesContractsAsync([futuresContract1, futuresContract2]);

            var result = await _testFixture.Db.GetFuturesContractsBySymbolAsync(symbol);
            result.Should().NotBeNull();
            result.Should().Contain(x => x.ContractId == futuresContract1.ContractId);
            result.Should().Contain(x => x.ContractId == futuresContract2.ContractId);
        }
        finally
        {
            await _testFixture.Db.DeleteFuturesContractAsync(futuresContract1.ContractId);
            await _testFixture.Db.DeleteFuturesContractAsync(futuresContract2.ContractId);
        }
    }

    [Fact]
    public async Task OnTheRunAndRolloverQueriesReturnDistinctOperationalRoles()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var symbol = $"CURRENT_{suffix}";
        var contracts = new[]
        {
            SampleData.FuturesContract with
            {
                ContractId = $"CURRENT_OLD_{suffix}",
                Symbol = symbol,
                LastTradeDate = new DateOnly(2026, 3, 20),
                OnTheRun = false,
                Rollover = false
            },
            SampleData.FuturesContract with
            {
                ContractId = $"CURRENT_NEW_{suffix}",
                Symbol = symbol,
                LastTradeDate = new DateOnly(2026, 6, 19),
                OnTheRun = true,
                Rollover = true
            },
            SampleData.FuturesContract with
            {
                ContractId = $"NOT_CURRENT_{suffix}",
                Symbol = symbol,
                LastTradeDate = new DateOnly(2026, 12, 18),
                OnTheRun = false,
                Rollover = true
            },
            SampleData.FuturesContract with
            {
                ContractId = $"DISTRACTOR_{suffix}",
                Symbol = $"OTHER_{suffix}",
                LastTradeDate = new DateOnly(2027, 3, 19),
                OnTheRun = true,
                Rollover = true
            }
        };

        await _testFixture.Db.InsertFuturesContractsAsync(contracts);

        var latest = await _testFixture.Db.GetOnTheRunFuturesContractAsync(symbol);
        var allCurrent = await _testFixture.Db.GetRolloverFuturesContractsAsync(symbol);

        latest.Should().NotBeNull();
        latest!.ContractId.Should().Be($"CURRENT_NEW_{suffix}");
        allCurrent.Select(contract => contract.ContractId).Should().Equal(
            $"CURRENT_NEW_{suffix}",
            $"NOT_CURRENT_{suffix}");

        await _testFixture.Db.DeleteOnTheRunFuturesContractAsync(symbol);

        var nextLatest = await _testFixture.Db.GetOnTheRunFuturesContractAsync(symbol);
        nextLatest.Should().BeNull();
    }

    [Fact]
    public async Task UpdateFuturesContractAsync_MovesSymbolProjectionAndDeleteRemovesIt()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var original = SampleData.FuturesContract with
        {
            ContractId = $"MOVE_{suffix}",
            Symbol = $"OLD_{suffix}",
            LastTradeDate = new DateOnly(2026, 9, 18),
            OnTheRun = true
        };
        var updated = original with
        {
            Symbol = $"NEW_{suffix}",
            LastTradeDate = new DateOnly(2026, 12, 18),
            OnTheRun = false
        };

        await _testFixture.Db.InsertFuturesContractAsync(original);
        await _testFixture.Db.UpdateFuturesContractAsync(
            new FuturesContractId(original.ContractId, original.Symbol, original.LastTradeDate),
            updated);

        (await _testFixture.Db.GetFuturesContractsBySymbolAsync(original.Symbol))
            .Should().NotContain(contract => contract.ContractId == original.ContractId);
        (await _testFixture.Db.GetFuturesContractsBySymbolAsync(updated.Symbol))
            .Should().ContainSingle(contract => contract.ContractId == updated.ContractId);

        await _testFixture.Db.DeleteFuturesContractAsync(
            new FuturesContractId(updated.ContractId, updated.Symbol, updated.LastTradeDate));

        (await _testFixture.Db.GetFuturesContractsBySymbolAsync(updated.Symbol))
            .Should().NotContain(contract => contract.ContractId == updated.ContractId);
    }

    [Fact]
    public async Task FuturesOptionSymbolProjection_IsIsolatedAndMaintainedAcrossUpdateAndDelete()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var original = SampleData.FuturesOptionContract with
        {
            ContractId = $"OT{suffix}20260619C2525",
            Symbol = $"OPT_OLD_{suffix}",
            ContractMonth = new DateOnly(2026, 6, 19)
        };
        var distractor = SampleData.FuturesOptionContract with
        {
            ContractId = $"OD{suffix}20260619P2515",
            Symbol = $"OPT_OTHER_{suffix}",
            ContractMonth = new DateOnly(2026, 6, 19)
        };
        var updated = original with
        {
            Symbol = $"OPT_NEW_{suffix}",
            ContractMonth = new DateOnly(2026, 9, 18),
            Description = "Updated projection"
        };

        await _testFixture.Db.InsertFuturesOptionContractsAsync([original, distractor]);

        (await _testFixture.Db.GetFuturesOptionContractsAsync(original.Symbol))
            .Should().ContainSingle(contract => contract.ContractId == original.ContractId);

        await _testFixture.Db.UpdateFuturesOptionContractAsync(original.ContractId, updated);

        (await _testFixture.Db.GetFuturesOptionContractsAsync(original.Symbol))
            .Should().NotContain(contract => contract.ContractId == original.ContractId);
        (await _testFixture.Db.GetFuturesOptionContractsAsync(updated.Symbol))
            .Should().ContainSingle(contract =>
                contract.ContractId == updated.ContractId &&
                contract.Description == updated.Description);

        await _testFixture.Db.DeleteFuturesOptionContractAsync(updated.ContractId);

        (await _testFixture.Db.GetFuturesOptionContractsAsync(updated.Symbol))
            .Should().NotContain(contract => contract.ContractId == updated.ContractId);
    }

    [Fact]
    public void MutationJournalCleanup_RetainsAmbiguousPostSubmissionOperations()
    {
        true.CanRemoveProjectionMutationJournalAfterFailure()
            .Should().BeFalse();
        false.CanRemoveProjectionMutationJournalAfterFailure()
            .Should().BeTrue();
        false.CanRemoveProjectionMutationJournalAfterFailure(
                activationResponseConfirmed: false)
            .Should().BeFalse("an activation/set-add timeout can apply after its caller observes failure");
    }

    [Fact]
    public void SecuritiesCql_DoesNotUseAllowFiltering()
    {
        var statements = typeof(SecuritiesDbCql)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!);

        statements.Should().NotContain(statement =>
            statement.Contains("ALLOW FILTERING", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProjectionCompletionSchema_UsesFreshGenerationAwareV3Tables()
    {
        SecuritiesSchemaCql.CreateSecuritiesProjectionStateV3Table
            .Should().Contain("securities_projection_state")
            .And.Contain("generation uuid")
            .And.Contain("completed boolean")
            .And.Contain("activeOperations set<uuid>");
        SecuritiesSchemaCql.CreateSecuritiesSymbolProjectionStateV3Table
            .Should().Contain("securities_symbol_projection_state")
            .And.Contain("generation uuid")
            .And.Contain("completed boolean")
            .And.Contain("activeOperations set<uuid>")
            .And.Contain("PRIMARY KEY ((projectionName, symbol))");
    }

    [Fact]
    public void SymbolProjectionReadFence_RequiresStableIdleGlobalAndSymbolGenerations()
    {
        var globalGeneration = Guid.NewGuid();
        var symbolGeneration = Guid.NewGuid();
        var context = (SecuritiesDbContext)RuntimeHelpers.GetUninitializedObject(typeof(SecuritiesDbContext));

        context.IsSymbolProjectionReadFenceCurrent(
                globalGeneration,
                globalGeneration,
                currentGlobalIsComplete: false,
                currentGlobalHasNoActiveOperations: true,
                symbolGeneration,
                symbolGeneration,
                currentSymbolIsComplete: true,
                currentSymbolHasNoActiveOperations: true)
            .Should().BeTrue("an idle incomplete global state permits lazy per-symbol readiness");

        context.IsSymbolProjectionReadFenceCurrent(
                globalGeneration,
                globalGeneration,
                currentGlobalIsComplete: false,
                currentGlobalHasNoActiveOperations: false,
                symbolGeneration,
                symbolGeneration,
                currentSymbolIsComplete: true,
                currentSymbolHasNoActiveOperations: true)
            .Should().BeFalse("an active global backfill fences every per-symbol stamp");

        context.IsSymbolProjectionReadFenceCurrent(
                globalGeneration,
                Guid.NewGuid(),
                currentGlobalIsComplete: false,
                currentGlobalHasNoActiveOperations: true,
                symbolGeneration,
                symbolGeneration,
                currentSymbolIsComplete: true,
                currentSymbolHasNoActiveOperations: true)
            .Should().BeFalse("a global generation change invalidates a captured per-symbol stamp");
    }

    [Fact]
    public void ProjectionOperationJournal_IsTimestampedScopedAndNeverExpiresAutomatically()
    {
        SecuritiesSchemaCql.CreateSecuritiesProjectionOperationV3Table
            .Should().Contain("securities_projection_operation")
            .And.Contain("startedOn timestamp")
            .And.Contain("stateMayBeActive boolean")
            .And.Contain("PRIMARY KEY ((projectionName), operationId)")
            .And.NotContain("TTL");
        SecuritiesSchemaCql.CreateSecuritiesProjectionOperationScopeV3Table
            .Should().Contain("securities_projection_operation_scope")
            .And.Contain("PRIMARY KEY ((projectionName, operationId), scopeType, scopeKey)")
            .And.NotContain("TTL");
        SecuritiesDbCql.InsertSecuritiesProjectionOperationV3
            .Should().Contain("stateMayBeActive)")
            .And.Contain("false)");
        SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3
            .Should().Contain("IF stateMayBeActive = :expectedStateMayBeActive");
        SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3
            .Should().Contain("DELETE activeOperations[:operationId]")
            .And.NotContain("IF EXISTS");
        SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3
            .Should().Contain("DELETE activeOperations[:operationId]")
            .And.NotContain("IF EXISTS");
    }

    [Fact]
    public void BackfillApi_AppendsOptionalUtcCutoffAfterCancellationToken()
    {
        var parameters = typeof(ISecuritiesDbWriteContext)
            .GetMethod(nameof(ISecuritiesDbWriteContext.BackfillSymbolProjectionsAsync))!
            .GetParameters();

        parameters.Select(parameter => parameter.Name).Should().Equal(
            "batchSize",
            "cancellationToken",
            "staleOperationCutoffUtc");
        parameters[2].ParameterType.Should().Be(typeof(DateTime?));
        parameters[2].HasDefaultValue.Should().BeTrue();
        parameters[2].DefaultValue.Should().BeNull();
    }

    [Fact]
    public void ProjectionOperationJournalBindValues_FollowCqlMarkerOrder()
    {
        const string projectionName = "futures_contract_by_symbol";
        const string symbol = "ES";
        var operationId = Guid.Parse("7a5733ab-374c-412f-bde8-75618ba91db6");
        var startedOn = new DateTime(2026, 8, 3, 14, 30, 0, DateTimeKind.Utc);
        var activeOperations = new HashSet<Guid> { operationId };

        AssertValues(
            new InsertSecuritiesProjectionOperationV3(projectionName, operationId, startedOn),
            projectionName, operationId, startedOn);
        AssertValues(
            new SetSecuritiesProjectionOperationStateMayBeActiveV3(
                true, projectionName, operationId, false),
            true, projectionName, operationId, false);
        AssertValues(
            new InsertSecuritiesProjectionOperationScopeV3(
                projectionName, operationId, "symbol", symbol),
            projectionName, operationId, "symbol", symbol);
        AssertValues(
            new GetSecuritiesProjectionOperationScopesV3(projectionName, operationId),
            projectionName, operationId);
        AssertValues(
            new RemoveSecuritiesProjectionOperationV3(operationId, projectionName),
            operationId, projectionName);
        AssertValues(
            new RemoveSecuritiesSymbolProjectionOperationV3(
                operationId, projectionName, symbol),
            operationId, projectionName, symbol);
    }

    [Fact]
    public void ProjectionCompletionCql_InvalidatesBeforeMutationAndCompletesConditionally()
    {
        SecuritiesDbCql.GetSecuritiesProjectionStateV3
            .Should().Contain("activeOperations");
        SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3
            .Should().Contain("activeOperations");
        SecuritiesDbCql.BeginSecuritiesProjectionOperationV3
            .Should().Contain("completed = false")
            .And.Contain("activeOperations = activeOperations + :activeOperations");
        SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3
            .Should().Contain("completed = false")
            .And.NotContain("completed = true");
        SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3
            .Should().Contain("completed = false")
            .And.Contain("activeOperations = activeOperations + :activeOperations");
        SecuritiesDbCql.CompleteSecuritiesProjectionOperationV3
            .Should().Contain("IF generation = :generation")
            .And.Contain("activeOperations = :expectedActiveOperations");
        SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3
            .Should().Contain("IF generation = :generation")
            .And.Contain("activeOperations = :expectedActiveOperations");
    }

    [Fact]
    public void ProjectionRepairCql_CanResetAWholeSymbolPartition()
    {
        SecuritiesDbCql.DeleteFuturesContractBySymbolV3Partition
            .Should().Contain("WHERE symbol = :symbol")
            .And.NotContain("contractId");
        SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition
            .Should().Contain("WHERE symbol = :symbol")
            .And.NotContain("contractId");
    }

    static void AssertValues(IBindValue bindValue, params object?[] expected)
        => ((object?[])bindValue.Bind()).Should().Equal(expected);
}
