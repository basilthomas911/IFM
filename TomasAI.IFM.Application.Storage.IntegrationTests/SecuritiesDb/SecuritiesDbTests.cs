using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TomasAI.IFM.Application.Blackboard;
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

    public SecuritiesDatabaseFixture()
    {
        var dbConn = new DbConnectionSettings()
                         .Add("SecuritiesDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=securities_test_db", "System.Data.ScyllaDb");

        var diContainer = new Dictionary<Type, SecuritiesDbContext>();
        var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
        var logger = Substitute.For<ILogger<DbProvider>>();
        logger.When(_ => { }).Do(_ => { });
        new TomasAI.IFM.Application.Storage.SecuritiesDb.Schema.SecuritiesSchemaDb(dbConn, logger)
            .CreateAllAsync().GetAwaiter().GetResult();
        var redisCache = Substitute.For<IRedisCache>();
        var redisCacheMap = new Dictionary<string, string>();
        redisCache.Get(Arg.Any<string>()).Returns(callInfo => redisCacheMap[callInfo.Arg<string>()]);
        redisCache.When(_ => _.Set(Arg.Any<string>(), Arg.Any<string>())).Do(_ => { redisCacheMap.Add(_.ArgAt<string>(0), _.ArgAt<string>(1)); });
        var blackboardServce = new BlackboardService(redisCache, new SystemTextJsonSerializer());
        var dbFactory = new DbContextFactory(dbResolver);
        var dbCache = new DbCache();
        DbFactory = dbFactory;
        diContainer.Add(typeof(IObjectRepository<SecuritiesDbContext>), new SecuritiesDbContext(dbConn, DbFactory, logger));
        Db = DbFactory.SecuritiesDb as SecuritiesDbContext;
        foreach (var table in new[]
        {
            "securities_projection_operation_scope_v3",
            "securities_projection_operation_v3",
            "securities_symbol_projection_state_v3",
            "securities_projection_state_v3"
        })
        {
            Db.Use($"TRUNCATE {table};").ExecuteCommandAsync().GetAwaiter().GetResult();
        }
    }

    public SecuritiesDbContext Db { get; }

    public IDbContextFactory DbFactory { get; }

    public void Dispose()
    {
    }
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
        var states = await db.Use(SecuritiesDbCql.GetSecuritiesProjectionStateV3)
            .SetParameters(new GetSecuritiesProjectionStateV3(projectionName))
            .ExecuteQueryAsync(static row => new ProjectionCompletionState(row.GetGuid(0), row.GetBool(1)));
        return states.Count == 1 ? states.First() : null;
    }

    static async Task<ProjectionCompletionState?> ReadSymbolProjectionStateAsync(
        IObjectRepository db,
        string projectionName,
        string symbol)
    {
        var states = await db.Use(SecuritiesDbCql.GetSecuritiesSymbolProjectionStateV3)
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
            await db.Use(SecuritiesDbCql.DeleteSecuritiesSymbolProjectionStateV3)
                .SetParameters(new DeleteSecuritiesSymbolProjectionStateV3(projectionName, symbol))
                .ExecuteCommandAsync();
            return;
        }

        await db.Use(SecuritiesDbCql.DeleteSecuritiesProjectionStateV3)
            .SetParameters(new DeleteSecuritiesProjectionStateV3(projectionName))
            .ExecuteCommandAsync();
    }

    static async Task ResetProjectionOperationsAsync(IObjectRepository db, string projectionName)
    {
        var operationIds = await db.Use(SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)
            .SetParameters(new GetSecuritiesProjectionOperationsV3(projectionName))
            .ExecuteQueryAsync(static row => row.GetGuid(0));
        foreach (var operationId in operationIds)
        {
            var scopes = await db.Use(SecuritiesDbCql.GetSecuritiesProjectionOperationScopesV3)
                .SetParameters(new GetSecuritiesProjectionOperationScopesV3(projectionName, operationId))
                .ExecuteQueryAsync(static row => (Type: row.GetString(0), Key: row.GetString(1)));
            await db.Use(SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)
                .SetParameters(new RemoveSecuritiesProjectionOperationV3(operationId, projectionName))
                .ExecuteCommandAsync();
            foreach (var symbol in scopes
                .Where(static scope => scope.Type == "symbol")
                .Select(static scope => scope.Key))
            {
                await db.Use(SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(new RemoveSecuritiesSymbolProjectionOperationV3(
                        operationId,
                        projectionName,
                        symbol))
                    .ExecuteCommandAsync();
            }
            await db.Use(SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)
                .SetParameters(new DeleteSecuritiesProjectionOperationScopesV3(projectionName, operationId))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)
                .SetParameters(new DeleteSecuritiesProjectionOperationV3(projectionName, operationId))
                .ExecuteCommandAsync();
        }
    }

    static InsertFuturesContract ToInsertParameters(FuturesContractV2ReadModel contract)
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
            contract.CurrentlyTraded);

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
    public async Task GetCurrentlyTradedFuturesContractAsync_ReturnsLatestCurrentContractOnly()
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
                CurrentlyTraded = true
            },
            SampleData.FuturesContract with
            {
                ContractId = $"CURRENT_NEW_{suffix}",
                Symbol = symbol,
                LastTradeDate = new DateOnly(2026, 6, 19),
                CurrentlyTraded = true
            },
            SampleData.FuturesContract with
            {
                ContractId = $"NOT_CURRENT_{suffix}",
                Symbol = symbol,
                LastTradeDate = new DateOnly(2026, 12, 18),
                CurrentlyTraded = false
            },
            SampleData.FuturesContract with
            {
                ContractId = $"DISTRACTOR_{suffix}",
                Symbol = $"OTHER_{suffix}",
                LastTradeDate = new DateOnly(2027, 3, 19),
                CurrentlyTraded = true
            }
        };

        await _testFixture.Db.InsertFuturesContractsAsync(contracts);

        var latest = await _testFixture.Db.GetCurrentlyTradedFuturesContractAsync(symbol);
        var allCurrent = await _testFixture.Db.GetCurrentlyTradedFuturesContractsAsync(symbol);

        latest.Should().NotBeNull();
        latest!.ContractId.Should().Be($"CURRENT_NEW_{suffix}");
        allCurrent.Select(contract => contract.ContractId).Should().Equal(
            $"CURRENT_NEW_{suffix}",
            $"CURRENT_OLD_{suffix}");

        await _testFixture.Db.DeleteCurrentlyTradedFuturesContractAsync(symbol);

        var nextLatest = await _testFixture.Db.GetCurrentlyTradedFuturesContractAsync(symbol);
        nextLatest.Should().NotBeNull();
        nextLatest!.ContractId.Should().Be($"CURRENT_OLD_{suffix}");
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
            CurrentlyTraded = true
        };
        var updated = original with
        {
            Symbol = $"NEW_{suffix}",
            LastTradeDate = new DateOnly(2026, 12, 18),
            CurrentlyTraded = false
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
    [Trait("securities projection migration", "partial-v2")]
    public async Task PartialNonEmptySymbolProjections_FallBackAndRepairExactCanonicalRows()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var futuresSymbol = $"PARTIAL_FUT_{suffix}";
        var optionSymbol = $"PARTIAL_OPT_{suffix}";
        var futuresContracts = new[]
        {
            SampleData.FuturesContract with
            {
                ContractId = $"PARTIAL_FUT_A_{suffix}",
                Symbol = futuresSymbol,
                LastTradeDate = new DateOnly(2026, 9, 18)
            },
            SampleData.FuturesContract with
            {
                ContractId = $"PARTIAL_FUT_B_{suffix}",
                Symbol = futuresSymbol,
                LastTradeDate = new DateOnly(2026, 12, 18)
            }
        };
        var optionContracts = new[]
        {
            SampleData.FuturesOptionContract with
            {
                ContractId = $"PA{suffix}20260918C2500",
                Symbol = optionSymbol,
                ContractMonth = new DateOnly(2026, 9, 18),
                StrikePrice = 2500
            },
            SampleData.FuturesOptionContract with
            {
                ContractId = $"PB{suffix}20261218C2600",
                Symbol = optionSymbol,
                ContractMonth = new DateOnly(2026, 12, 18),
                StrikePrice = 2600
            }
        };
        var db = _testFixture.Db;

        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection);
        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, futuresSymbol);
        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection, optionSymbol);
        try
        {
            await db.Use(SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(futuresContracts.Select(ToInsertParameters))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(optionContracts.Select(ToInsertParameters))
                .ExecuteCommandAsync();

            // A non-empty target is deliberately incomplete. Contents alone must not authorize reads.
            await db.Use(SecuritiesDbCql.InsertFuturesContractBySymbolV2)
                .SetParameters(ToInsertParameters(futuresContracts[0]))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(ToInsertParameters(optionContracts[0]))
                .ExecuteCommandAsync();

            (await db.GetFuturesContractsBySymbolAsync(futuresSymbol))
                .Select(static contract => contract.ContractId)
                .Should().BeEquivalentTo(futuresContracts.Select(static contract => contract.ContractId));
            (await db.GetFuturesOptionContractsAsync(optionSymbol))
                .Select(static contract => contract.ContractId)
                .Should().BeEquivalentTo(optionContracts.Select(static contract => contract.ContractId));

            var projectedFuturesIds = await db.Use(SecuritiesDbCql.GetFuturesContractsBySymbol)
                .SetParameters(new GetFuturesContractsBySymbol(futuresSymbol))
                .ExecuteQueryAsync(static row => row.GetString(0));
            var projectedOptionIds = await db.Use(SecuritiesDbCql.GetFuturesOptionContractsBySymbol)
                .SetParameters(new GetFuturesOptionContractsBySymbol(optionSymbol))
                .ExecuteQueryAsync(static row => row.GetString(0));
            projectedFuturesIds.Should().BeEquivalentTo(
                futuresContracts.Select(static contract => contract.ContractId));
            projectedOptionIds.Should().BeEquivalentTo(
                optionContracts.Select(static contract => contract.ContractId));

            (await ReadSymbolProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                futuresSymbol))!.Value.Completed.Should().BeTrue();
            (await ReadSymbolProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesOptionContractSymbolProjection,
                optionSymbol))!.Value.Completed.Should().BeTrue();
        }
        finally
        {
            await db.Use(SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(futuresContracts.Select(static contract => new DeleteFuturesContract(contract.ContractId)))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesOptionContract)
                .SetParameters(optionContracts.Select(static contract => new DeleteFuturesOptionContract(contract.ContractId)))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesContractBySymbolV2Partition(futuresSymbol))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesOptionContractBySymbolV2Partition(optionSymbol))
                .ExecuteCommandAsync();
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, futuresSymbol);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection, optionSymbol);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection);
        }
    }

    [Fact]
    [Trait("securities projection migration", "empty-symbol")]
    public async Task UnknownSymbol_FallbackRecordsPerSymbolCompletionWithoutGlobalCutover()
    {
        var symbol = $"UNKNOWN_{Guid.NewGuid():N}";
        var db = _testFixture.Db;

        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, symbol);
        try
        {
            (await db.GetFuturesContractsBySymbolAsync(symbol)).Should().BeEmpty();

            var symbolState = await ReadSymbolProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                symbol);
            symbolState.Should().NotBeNull();
            symbolState!.Value.Completed.Should().BeTrue();

            var globalState = await ReadProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection);
            globalState.Should().NotBeNull();
            globalState!.Value.Completed.Should().BeFalse();

            // The second read is authorized by the empty-symbol marker rather than another full scan.
            (await db.GetFuturesContractsBySymbolAsync(symbol)).Should().BeEmpty();
        }
        finally
        {
            await db.Use(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesContractBySymbolV2Partition(symbol))
                .ExecuteCommandAsync();
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, symbol);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        }
    }

    [Fact]
    [Trait("securities projection migration", "concurrency")]
    public async Task ConcurrentMutation_PreventsFallbackFromPublishingCompletion()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var contract = SampleData.FuturesContract with
        {
            ContractId = $"RACE_FUT_{suffix}",
            Symbol = $"RACE_SYMBOL_{suffix}",
            LastTradeDate = new DateOnly(2027, 6, 18)
        };
        var db = _testFixture.Db;
        var writerOperationId = Guid.NewGuid();
        var writerOperations = new HashSet<Guid> { writerOperationId };
        var writerActive = false;

        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        await DeleteProjectionStateAsync(
            db,
            SecuritiesDbContext.FuturesContractSymbolProjection,
            contract.Symbol);
        try
        {
            await db.Use(SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(ToInsertParameters(contract))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.BeginSecuritiesProjectionOperationV3)
                .SetParameters(new BeginSecuritiesProjectionOperationV3(
                    writerOperationId,
                    writerOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new BeginSecuritiesSymbolProjectionOperationV3(
                    writerOperationId,
                    writerOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    contract.Symbol))
                .ExecuteCommandAsync();
            writerActive = true;

            (await db.GetFuturesContractsBySymbolAsync(contract.Symbol))
                .Should().ContainSingle(candidate => candidate.ContractId == contract.ContractId);
            (await ReadSymbolProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                contract.Symbol))!.Value.Completed.Should().BeFalse(
                    "a fallback cannot publish while another operation is active for the symbol");

            var endGeneration = Guid.NewGuid();
            await db.Use(SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new EndSecuritiesSymbolProjectionOperationV3(
                    endGeneration,
                    writerOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    contract.Symbol))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.EndSecuritiesProjectionOperationV3)
                .SetParameters(new EndSecuritiesProjectionOperationV3(
                    endGeneration,
                    writerOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteCommandAsync();
            writerActive = false;

            (await db.GetFuturesContractsBySymbolAsync(contract.Symbol))
                .Should().ContainSingle(candidate => candidate.ContractId == contract.ContractId);
            (await ReadSymbolProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                contract.Symbol))!.Value.Completed.Should().BeTrue(
                    "a retry can publish only after the competing operation has ended");
        }
        finally
        {
            if (writerActive)
            {
                var endGeneration = Guid.NewGuid();
                await db.Use(SecuritiesDbCql.EndSecuritiesSymbolProjectionOperationV3)
                    .SetParameters(new EndSecuritiesSymbolProjectionOperationV3(
                        endGeneration,
                        writerOperations,
                        SecuritiesDbContext.FuturesContractSymbolProjection,
                        contract.Symbol))
                    .ExecuteCommandAsync();
                await db.Use(SecuritiesDbCql.EndSecuritiesProjectionOperationV3)
                    .SetParameters(new EndSecuritiesProjectionOperationV3(
                        endGeneration,
                        writerOperations,
                        SecuritiesDbContext.FuturesContractSymbolProjection))
                    .ExecuteCommandAsync();
            }

            await db.Use(SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(new DeleteFuturesContract(contract.ContractId))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesContractBySymbolV2Partition(contract.Symbol))
                .ExecuteCommandAsync();
            await DeleteProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                contract.Symbol);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        }
    }

    [Fact]
    [Trait("securities projection migration", "global-read-fence")]
    public async Task ActiveGlobalBackfill_FencesAnOtherwiseCompletedSymbolProjection()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var canonical = SampleData.FuturesContract with
        {
            ContractId = $"GLOBAL_FENCE_FUT_{suffix}",
            Symbol = $"GLOBAL_FENCE_SYMBOL_{suffix}",
            Description = $"canonical-{suffix}",
            LastTradeDate = new DateOnly(2029, 6, 15)
        };
        var staleProjection = canonical with { Description = $"stale-{suffix}" };
        var db = _testFixture.Db;
        var symbolOperationId = Guid.NewGuid();
        var symbolOperations = new HashSet<Guid> { symbolOperationId };
        var globalOperationId = Guid.NewGuid();
        var globalOperations = new HashSet<Guid> { globalOperationId };
        var globalOperationActive = false;

        await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        await DeleteProjectionStateAsync(
            db,
            SecuritiesDbContext.FuturesContractSymbolProjection,
            canonical.Symbol);
        try
        {
            await db.Use(SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(ToInsertParameters(canonical))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertFuturesContractBySymbolV2)
                .SetParameters(ToInsertParameters(staleProjection))
                .ExecuteCommandAsync();

            await db.Use(SecuritiesDbCql.InvalidateSecuritiesProjectionStateV3)
                .SetParameters(new InvalidateSecuritiesProjectionStateV3(
                    Guid.NewGuid(),
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new BeginSecuritiesSymbolProjectionOperationV3(
                    symbolOperationId,
                    symbolOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    canonical.Symbol))
                .ExecuteCommandAsync();
            var symbolCompleted = await db.Use(SecuritiesDbCql.CompleteSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new CompleteSecuritiesSymbolProjectionOperationV3(
                    symbolOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    canonical.Symbol,
                    symbolOperationId,
                    symbolOperations))
                .ExecuteScalarAsync(static row => row.GetBool(0));
            symbolCompleted.Should().BeTrue();

            await db.Use(SecuritiesDbCql.BeginSecuritiesProjectionOperationV3)
                .SetParameters(new BeginSecuritiesProjectionOperationV3(
                    globalOperationId,
                    globalOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteCommandAsync();
            globalOperationActive = true;

            var result = await db.GetFuturesContractsBySymbolAsync(canonical.Symbol);

            result.Should().ContainSingle(contract =>
                contract.ContractId == canonical.ContractId &&
                contract.Description == canonical.Description);
            result.Should().NotContain(contract => contract.Description == staleProjection.Description,
                "a per-symbol completion cannot bypass an active global backfill fence");
        }
        finally
        {
            if (globalOperationActive)
            {
                await db.Use(SecuritiesDbCql.EndSecuritiesProjectionOperationV3)
                    .SetParameters(new EndSecuritiesProjectionOperationV3(
                        Guid.NewGuid(),
                        globalOperations,
                        SecuritiesDbContext.FuturesContractSymbolProjection))
                    .ExecuteCommandAsync();
            }
            await db.Use(SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(new DeleteFuturesContract(canonical.ContractId))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesContractBySymbolV2Partition(canonical.Symbol))
                .ExecuteCommandAsync();
            await DeleteProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                canonical.Symbol);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        }
    }

    [Fact]
    [Trait("securities projection migration", "ambiguity")]
    public async Task BackfillSymbolProjectionsAsync_AmbiguousCanonicalIdFailsWithCutoverIncomplete()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var contractId = $"AMBIGUOUS_FUT_{suffix}";
        var contracts = new[]
        {
            SampleData.FuturesContract with
            {
                ContractId = contractId,
                Symbol = $"AMBIGUOUS_A_{suffix}",
                LastTradeDate = new DateOnly(2028, 3, 17)
            },
            SampleData.FuturesContract with
            {
                ContractId = contractId,
                Symbol = $"AMBIGUOUS_B_{suffix}",
                LastTradeDate = new DateOnly(2028, 6, 16)
            }
        };
        var db = _testFixture.Db;

        try
        {
            await db.Use(SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(contracts.Select(ToInsertParameters))
                .ExecuteCommandAsync();

            Func<Task> backfill = async () => await db.BackfillSymbolProjectionsAsync(batchSize: 64);
            await backfill.Should().ThrowAsync<StorageException>()
                .WithMessage($"*{contractId}*");

            var globalState = await ReadProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection);
            globalState.Should().NotBeNull();
            globalState!.Value.Completed.Should().BeFalse(
                "identity validation must happen while global cutover is disabled");
        }
        finally
        {
            await db.Use(SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(new DeleteFuturesContract(contractId))
                .ExecuteCommandAsync();
            foreach (var contract in contracts)
            {
                await db.Use(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                    .SetParameters(new DeleteFuturesContractBySymbolV2Partition(contract.Symbol))
                    .ExecuteCommandAsync();
                await DeleteProjectionStateAsync(
                    db,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    contract.Symbol);
            }
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection);
        }
    }

    [Fact]
    [Trait("securities projection migration", "backfill")]
    public async Task BackfillSymbolProjectionsAsync_RemovesStaleMappingsAndPublishesCutoverLast()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var futuresContract = SampleData.FuturesContract with
        {
            ContractId = $"BACKFILL_FUT_{suffix}",
            Symbol = $"BACKFILL_FUT_SYMBOL_{suffix}",
            LastTradeDate = new DateOnly(2027, 3, 19)
        };
        var optionContract = SampleData.FuturesOptionContract with
        {
            ContractId = $"BF{suffix}20270319C1000",
            Symbol = $"BACKFILL_OPT_SYMBOL_{suffix}",
            ContractMonth = new DateOnly(2027, 3, 19)
        };
        var staleFutures = futuresContract with { Symbol = $"STALE_FUT_SYMBOL_{suffix}" };
        var staleOption = optionContract with { Symbol = $"STALE_OPT_SYMBOL_{suffix}" };
        var db = _testFixture.Db;

        await ResetProjectionOperationsAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        await ResetProjectionOperationsAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection);

        try
        {
            await db.Use(SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(ToInsertParameters(futuresContract))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertFuturesOptionContract)
                .SetParameters(ToInsertParameters(optionContract))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertFuturesContractBySymbolV2)
                .SetParameters(ToInsertParameters(staleFutures))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertFuturesOptionContractBySymbolV2)
                .SetParameters(ToInsertParameters(staleOption))
                .ExecuteCommandAsync();

            var result = await db.BackfillSymbolProjectionsAsync(batchSize: 64);
            var reconciliation = await db.ReconcileSymbolProjectionsAsync();

            result.FuturesContractsUpserted.Should().BeGreaterThan(0);
            result.FuturesOptionContractsUpserted.Should().BeGreaterThan(0);
            reconciliation.IsConsistent.Should().BeTrue();
            (await ReadProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection))!.Value.Completed.Should().BeTrue();
            (await ReadProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesOptionContractSymbolProjection))!.Value.Completed.Should().BeTrue();

            (await db.Use(SecuritiesDbCql.GetFuturesContractsBySymbol)
                .SetParameters(new GetFuturesContractsBySymbol(staleFutures.Symbol))
                .ExecuteQueryAsync(static row => row.GetString(0))).Should().BeEmpty();
            (await db.Use(SecuritiesDbCql.GetFuturesOptionContractsBySymbol)
                .SetParameters(new GetFuturesOptionContractsBySymbol(staleOption.Symbol))
                .ExecuteQueryAsync(static row => row.GetString(0))).Should().BeEmpty();
            (await db.GetFuturesContractsBySymbolAsync(futuresContract.Symbol))
                .Should().ContainSingle(contract => contract.ContractId == futuresContract.ContractId);
            (await db.GetFuturesOptionContractsAsync(optionContract.Symbol))
                .Should().ContainSingle(contract => contract.ContractId == optionContract.ContractId);
        }
        finally
        {
            await db.Use(SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(new DeleteFuturesContract(futuresContract.ContractId))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesOptionContract)
                .SetParameters(new DeleteFuturesOptionContract(optionContract.ContractId))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                .SetParameters(new[]
                {
                    new DeleteFuturesContractBySymbolV2Partition(futuresContract.Symbol),
                    new DeleteFuturesContractBySymbolV2Partition(staleFutures.Symbol)
                })
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition)
                .SetParameters(new[]
                {
                    new DeleteFuturesOptionContractBySymbolV2Partition(optionContract.Symbol),
                    new DeleteFuturesOptionContractBySymbolV2Partition(staleOption.Symbol)
                })
                .ExecuteCommandAsync();
            foreach (var symbol in new[] { futuresContract.Symbol, staleFutures.Symbol })
                await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection, symbol);
            foreach (var symbol in new[] { optionContract.Symbol, staleOption.Symbol })
                await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection, symbol);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection);
        }
    }

    [Fact]
    [Trait("securities projection migration", "stale operation recovery")]
    public async Task BackfillSymbolProjectionsAsync_RecoversOnlyExplicitlyCutOffJournaledOperation()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var contract = SampleData.FuturesContract with
        {
            ContractId = $"STALE_OPERATION_FUT_{suffix}",
            Symbol = $"STALE_OPERATION_SYMBOL_{suffix}",
            LastTradeDate = new DateOnly(2029, 3, 16)
        };
        var db = _testFixture.Db;
        var operationId = Guid.NewGuid();
        var inertOperationId = Guid.NewGuid();
        var activeOperations = new HashSet<Guid> { operationId };
        var startedOn = DateTime.UtcNow.AddHours(-2);

        await ResetProjectionOperationsAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
        await ResetProjectionOperationsAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection);

        try
        {
            await db.Use(SecuritiesDbCql.InsertFuturesContract)
                .SetParameters(ToInsertParameters(contract))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertSecuritiesProjectionOperationScopeV3)
                .SetParameters(new[]
                {
                    new InsertSecuritiesProjectionOperationScopeV3(
                        SecuritiesDbContext.FuturesContractSymbolProjection,
                        operationId,
                        "global",
                        SecuritiesDbContext.FuturesContractSymbolProjection),
                    new InsertSecuritiesProjectionOperationScopeV3(
                        SecuritiesDbContext.FuturesContractSymbolProjection,
                        operationId,
                        "symbol",
                        contract.Symbol),
                    new InsertSecuritiesProjectionOperationScopeV3(
                        SecuritiesDbContext.FuturesContractSymbolProjection,
                        operationId,
                        "scope-count",
                        "2")
                })
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.InsertSecuritiesProjectionOperationV3)
                .SetParameters(new InsertSecuritiesProjectionOperationV3(
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    operationId,
                    startedOn))
                .ExecuteCommandAsync();
            (await db.Use(SecuritiesDbCql.SetSecuritiesProjectionOperationStateMayBeActiveV3)
                .SetParameters(new SetSecuritiesProjectionOperationStateMayBeActiveV3(
                    true,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    operationId,
                    false))
                .ExecuteSingleAsync(static row => row.GetBool(0))).Should().BeTrue();
            await db.Use(SecuritiesDbCql.InsertSecuritiesProjectionOperationV3)
                .SetParameters(new InsertSecuritiesProjectionOperationV3(
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    inertOperationId,
                    startedOn))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.BeginSecuritiesProjectionOperationV3)
                .SetParameters(new BeginSecuritiesProjectionOperationV3(
                    operationId,
                    activeOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.BeginSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new BeginSecuritiesSymbolProjectionOperationV3(
                    operationId,
                    activeOperations,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    contract.Symbol))
                .ExecuteCommandAsync();

            Func<Task> withoutOperatorCutoff = async () =>
                await db.BackfillSymbolProjectionsAsync(batchSize: 64);
            await withoutOperatorCutoff.Should().ThrowAsync<StorageException>();

            var retainedOperations = await db.Use(SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)
                .SetParameters(new GetSecuritiesProjectionOperationsV3(
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteQueryAsync(static row => row.GetGuid(0));
            retainedOperations.Should().Contain(operationId,
                "an unclassified operation must never be cleared automatically");
            retainedOperations.Should().Contain(inertOperationId,
                "even an inert torn journal is cleared only with explicit operator intent");

            SecuritiesProjectionBackfillResult? result = null;
            try
            {
                result = await db.BackfillSymbolProjectionsAsync(
                    batchSize: 64,
                    staleOperationCutoffUtc: DateTime.UtcNow.AddHours(-1));
            }
            catch (StorageException exception) when (
                exception.Message.Contains("maps to multiple", StringComparison.Ordinal))
            {
                // A shared developer test database can contain ambiguity left by older
                // tests. Recovery intentionally runs before canonical validation, so the
                // journal assertions below remain deterministic in either database state.
            }

            if (result is not null)
            {
                result.FuturesContractsUpserted.Should().BeGreaterThan(0);
                (await db.GetFuturesContractsBySymbolAsync(contract.Symbol))
                    .Should().ContainSingle(candidate => candidate.ContractId == contract.ContractId);
            }
            var remainingOperations = await db.Use(SecuritiesDbCql.GetSecuritiesProjectionOperationsV3)
                .SetParameters(new GetSecuritiesProjectionOperationsV3(
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteQueryAsync(static row => row.GetGuid(0));
            remainingOperations.Should().NotContain(operationId);
            remainingOperations.Should().NotContain(inertOperationId);
        }
        finally
        {
            await db.Use(SecuritiesDbCql.RemoveSecuritiesSymbolProjectionOperationV3)
                .SetParameters(new RemoveSecuritiesSymbolProjectionOperationV3(
                    operationId,
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    contract.Symbol))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.RemoveSecuritiesProjectionOperationV3)
                .SetParameters(new RemoveSecuritiesProjectionOperationV3(
                    operationId,
                    SecuritiesDbContext.FuturesContractSymbolProjection))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteSecuritiesProjectionOperationScopesV3)
                .SetParameters(new DeleteSecuritiesProjectionOperationScopesV3(
                    SecuritiesDbContext.FuturesContractSymbolProjection,
                    operationId))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteSecuritiesProjectionOperationV3)
                .SetParameters(new[]
                {
                    new DeleteSecuritiesProjectionOperationV3(
                        SecuritiesDbContext.FuturesContractSymbolProjection,
                        operationId),
                    new DeleteSecuritiesProjectionOperationV3(
                        SecuritiesDbContext.FuturesContractSymbolProjection,
                        inertOperationId)
                })
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesContract)
                .SetParameters(new DeleteFuturesContract(contract.ContractId))
                .ExecuteCommandAsync();
            await db.Use(SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition)
                .SetParameters(new DeleteFuturesContractBySymbolV2Partition(contract.Symbol))
                .ExecuteCommandAsync();
            await DeleteProjectionStateAsync(
                db,
                SecuritiesDbContext.FuturesContractSymbolProjection,
                contract.Symbol);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesContractSymbolProjection);
            await DeleteProjectionStateAsync(db, SecuritiesDbContext.FuturesOptionContractSymbolProjection);
        }
    }
}

public class SecuritiesCqlTests
{
    [Fact]
    public void MutationJournalCleanup_RetainsAmbiguousPostSubmissionOperations()
    {
        ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted: true)
            .Should().BeFalse();
        ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted: false)
            .Should().BeTrue();
        ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted: false,
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
            .Should().Contain("securities_projection_state_v3")
            .And.Contain("generation uuid")
            .And.Contain("completed boolean")
            .And.Contain("activeOperations set<uuid>");
        SecuritiesSchemaCql.CreateSecuritiesSymbolProjectionStateV3Table
            .Should().Contain("securities_symbol_projection_state_v3")
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

        SecuritiesDbContext.IsSymbolProjectionReadFenceCurrent(
                globalGeneration,
                globalGeneration,
                currentGlobalIsComplete: false,
                currentGlobalHasNoActiveOperations: true,
                symbolGeneration,
                symbolGeneration,
                currentSymbolIsComplete: true,
                currentSymbolHasNoActiveOperations: true)
            .Should().BeTrue("an idle incomplete global state permits lazy per-symbol readiness");

        SecuritiesDbContext.IsSymbolProjectionReadFenceCurrent(
                globalGeneration,
                globalGeneration,
                currentGlobalIsComplete: false,
                currentGlobalHasNoActiveOperations: false,
                symbolGeneration,
                symbolGeneration,
                currentSymbolIsComplete: true,
                currentSymbolHasNoActiveOperations: true)
            .Should().BeFalse("an active global backfill fences every per-symbol stamp");

        SecuritiesDbContext.IsSymbolProjectionReadFenceCurrent(
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
            .Should().Contain("securities_projection_operation_v3")
            .And.Contain("startedOn timestamp")
            .And.Contain("stateMayBeActive boolean")
            .And.Contain("PRIMARY KEY ((projectionName), operationId)")
            .And.NotContain("TTL");
        SecuritiesSchemaCql.CreateSecuritiesProjectionOperationScopeV3Table
            .Should().Contain("securities_projection_operation_scope_v3")
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
        const string projectionName = "futures_contract_by_symbol_v2";
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
        SecuritiesDbCql.DeleteFuturesContractBySymbolV2Partition
            .Should().Contain("WHERE symbol = :symbol")
            .And.NotContain("contractId");
        SecuritiesDbCql.DeleteFuturesOptionContractBySymbolV2Partition
            .Should().Contain("WHERE symbol = :symbol")
            .And.NotContain("contractId");
    }

    static void AssertValues(IBindValue bindValue, params object?[] expected)
        => ((object?[])bindValue.Bind()).Should().Equal(expected);
}
