using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Fund.Queries;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.FundDb;

namespace TomasAI.IFM.Application.Query.UnitTests
{
    public class FundDatabaseFixture : IDisposable
    {

        public FundDatabaseFixture()
        {
            var dbConn = new DbConnectionSettings()
                .Add("FundDbConnection", @"Data Source=DEV-SERVER;Initial Catalog=funddb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient");
            var diContainer = new Dictionary<Type, FundDbContext>();
            var dbResolver = new DbContextResolver(repoType => diContainer[repoType]);
            DbFactory = new DbContextFactory(dbResolver);
            diContainer.Add(typeof(IObjectRepository<FundDbContext>), new FundDbContext(dbConn, DbFactory));
            Db = DbFactory.FundDb as FundDbContext;
        }
        public FundDbContext Db { get; }

        public IDbContextFactory DbFactory { get; }

        public void Dispose()
        {
        }
    }

    /*
        IAsyncQueryHandler<GetFundsQuery, FundReadModel[]>,
        IAsyncQueryHandler<GetFundTransactionsQuery, FundTransactionReadModel[]>,
        IAsyncQueryHandler<GetFundBalanceQuery, FundBalanceReadModel>,
        IAsyncQueryHandler<GetOpeningFundBalanceQuery, FundBalanceReadModel>,
        IAsyncQueryHandler<GetClosingFundBalanceQuery, FundBalanceReadModel>,
        IAsyncQueryHandler<GetFundPnlReportQuery, FundPnlReportReadModel>

     */
    public class FundQueryTests : IClassFixture<FundDatabaseFixture>
    {
        private FundDatabaseFixture _fixture;

        public FundQueryTests(FundDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task GetFundsQueryOk()
        {
            // given...
            await _fixture.Db.Use($"delete from fund where FundId = {SampleData.Fund.FundId}").ExecuteCommandAsync();
            await _fixture.Db.DbWriter.InsertFundAsync(SampleData.Fund);

            // when...
            var q = new GetFundsQuery { };
            var fundQuery = new FundQueries(_fixture.DbFactory);
            var funds = await fundQuery.ExecuteAsync(q);

            // then...
            funds.Should().NotBeNull();
            funds.Length.Should().Be(1);
            SampleData.Fund.FundId.Should().Be(funds[0].FundId);
            SampleData.Fund.Name.Should().Be(funds[0].Name);
            SampleData.Fund.Description.Should().Be(funds[0].Description);
            $"{SampleData.Fund.Balance:F2}".Should().Be($"{funds[0].Balance:F2}");
            SampleData.Fund.CreatedOn.Year.Should().Be(funds[0].CreatedOn.Year);
            SampleData.Fund.CreatedOn.Month.Should().Be(funds[0].CreatedOn.Month);
            SampleData.Fund.CreatedOn.Day.Should().Be(funds[0].CreatedOn.Day);
            SampleData.Fund.CreatedBy.Should().Be(funds[0].CreatedBy);
        }

        [Fact]
        public async Task GetFundBalanceQueryOk()
        {
            // given...
            await _fixture.Db.Use($"delete from fund where FundId = {SampleData.Fund.FundId}").ExecuteCommandAsync();
            await _fixture.Db.DbWriter.InsertFundAsync(SampleData.Fund);

            // when...
            var q = new GetFundBalanceQuery  {
                FundId = SampleData.Fund.FundId,
            };
            var fundQuery = new FundQueries(_fixture.DbFactory);
            var fundBalance = await fundQuery.ExecuteAsync(q);

            // then...
            fundBalance.Should().NotBeNull();
            $"{SampleData.Fund.Balance:F2}".Should().Be($"{fundBalance.Value:F2}");
        }

        [Fact]
        public async Task GetOpeningFundBalanceQueryOk()
        {
            // given...
            await _fixture.Db.Use($"delete from fund_transaction where FundId = {SampleData.FundTransactionByStatus(TradeStatus.Open).FundId}").ExecuteCommandAsync();
            await _fixture.Db.DbWriter.InsertFundTransactionAsync(SampleData.FundTransactionByStatus(TradeStatus.Open));

            // when...
            var q = new GetOpeningFundBalanceQuery
            {
                FundId = SampleData.FundTransactionByStatus(TradeStatus.Open).FundId,
                ValueDate = SampleData.FundTransactionByStatus(TradeStatus.Open).ValueDate
            };

            var fundQuery = new FundQueries(_fixture.DbFactory);
            var fundBalance = await fundQuery.ExecuteAsync(q);

            // then...
            fundBalance.Should().NotBeNull();
            $"{SampleData.FundTransactionByStatus(TradeStatus.Open).Balance:F2}".Should().Be( $"{fundBalance.Value:F2}");
        }

        [Fact]
        public async Task GetClosingFundBalanceQueryOk()
        {
            // given...
            await _fixture.Db.Use($"delete from fund_transaction where FundId = {SampleData.FundTransactionByStatus(TradeStatus.Close).FundId}").ExecuteCommandAsync();
            await _fixture.Db.DbWriter.InsertFundTransactionAsync(SampleData.FundTransactionByStatus(TradeStatus.Close));

            // when...
            var q = new GetClosingFundBalanceQuery
            {
                FundId = SampleData.FundTransactionByStatus(TradeStatus.Close).FundId,
                ValueDate = SampleData.FundTransactionByStatus(TradeStatus.Close).ValueDate
            };

            var fundQuery = new FundQueries(_fixture.DbFactory);
            var fundBalance = await fundQuery.ExecuteAsync(q);

            // then...
            fundBalance.Should().NotBeNull();
            $"{SampleData.FundTransactionByStatus(TradeStatus.Close).Balance:F2}".Should().Be($"{fundBalance.Value:F2}");
        }

        [Fact]
        public async Task GetFundTransactionsQueryOk()
        {
            // given...
            await _fixture.Db.Use($"delete from fund_transaction where FundId = {SampleData.FundTransaction.FundId}").ExecuteCommandAsync();
            await _fixture.Db.DbWriter.InsertFundTransactionAsync(SampleData.FundTransaction);

            // when...
            var q = new GetFundTransactionsQuery
            {
                FundId = SampleData.FundTransaction.FundId,
                StartDate = SampleData.FundTransaction.TransactionDate.AddDays(-1),
                EndDate = SampleData.FundTransaction.TransactionDate.AddDays(1)
            };
            var fundQuery = new FundQueries(_fixture.DbFactory);
            var fundTransactions = await fundQuery.ExecuteAsync(q);

            // then...
            fundTransactions.Should().NotBeNull();
            fundTransactions.Length.Should().Be(1);
            SampleData.FundTransaction.FundId.Should().Be(fundTransactions[0].FundId);
            SampleData.FundTransaction.OrderId.Should().Be(fundTransactions[0].OrderId);
            SampleData.FundTransaction.TradeId.Should().Be(fundTransactions[0].TradeId);
            SampleData.FundTransaction.TradeType.Should().Be(fundTransactions[0].TradeType);
            SampleData.FundTransaction.TradeStatus.Should().Be(fundTransactions[0].TradeStatus);
            SampleData.FundTransaction.Description.Should().Be(fundTransactions[0].Description);
            $"{SampleData.FundTransaction.Amount:F2}".Should().Be($"{fundTransactions[0].Amount:F2}");
            $"{SampleData.FundTransaction.Balance:F2}".Should().Be($"{fundTransactions[0].Balance:F2}");
            SampleData.FundTransaction.ValueDate.Year.Should().Be(fundTransactions[0].ValueDate.Year);
            SampleData.FundTransaction.ValueDate.Month.Should().Be(fundTransactions[0].ValueDate.Month);
            SampleData.FundTransaction.ValueDate.Day.Should().Be(fundTransactions[0].ValueDate.Day);
        }

        [Fact]
        public async Task GetFundPnlReportQueryOk()
        {
            var q = new GetFundPnlReportQuery
            {
                FundId = 1012,
                StartDate = new DateTime(2021, 1, 1),
                EndDate = new DateTime(2021, 12, 31)
            };
            var fundQuery = new FundQueries(_fixture.DbFactory);
            var report = await fundQuery.ExecuteAsync(q);
            Assert.NotNull(report);
        }
    }
}
