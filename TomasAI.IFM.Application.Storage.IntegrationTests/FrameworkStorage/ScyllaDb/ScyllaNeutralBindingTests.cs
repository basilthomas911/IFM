using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

public sealed class ScyllaNeutralBindingTests
{
    [Fact]
    public async Task Ordinary_and_neutral_values_share_the_same_prepared_statement_without_mutation()
    {
        var keyspace = "ifm_bind_" + Guid.NewGuid().ToString("N");
        var logger = Substitute.For<ILogger<DbProvider>>();
        var settings = new DbConnectionSettings()
            .Add("admin", "Contact Points=localhost;Port=9042;Default Keyspace=system", "System.Data.ScyllaDb")
            .Add("test", $"Contact Points=localhost;Port=9042;Default Keyspace={keyspace}", "System.Data.ScyllaDb");
        var admin = new ScyllaTestRepository(settings["admin"], logger);
        await admin.Use("Neutral.Create", $"CREATE KEYSPACE {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}}").ExecuteCommandAsync();
        try
        {
            var repository = new ScyllaTestRepository(settings["test"], logger);
            await repository.Use("Neutral.Table", "CREATE TABLE binding_test (id int PRIMARY KEY, label text, amount decimal, day date)").ExecuteCommandAsync();
            var date = new DateOnly(2026, 10, 30);
            object?[] values = [BindValues.Integer(1), BindValues.Text(null), BindValues.Numeric(1.25m), BindValues.Date(date)];
            const string insert = "INSERT INTO binding_test (id,label,amount,day) VALUES (?,?,?,?)";
            await repository.Use("Neutral.Insert", insert).SetParameters(new Parameters(values)).ExecuteCommandAsync();
            await repository.Use("Neutral.Insert", insert).SetParameters(new Parameters([2, "ordinary", 2.5m, date])).ExecuteCommandAsync();
            var rows = await repository.Use("Neutral.Read", "SELECT id,amount,day FROM binding_test WHERE id IN (?,?)")
                .SetParameters(new Parameters([1, BindValues.Integer(2)]))
                .ExecuteQueryAsync(row => (Id: row.GetInt(0), Amount: row.GetDecimal(1), Day: row.GetDateOnly(2)));
            Assert.Contains(rows, row => row.Id == 1 && row.Amount == 1.25m && row.Day == date);
            Assert.Contains(rows, row => row.Id == 2 && row.Amount == 2.5m && row.Day == date);
            Assert.IsType<BindParameter>(values[0]);
            Assert.IsType<BindParameter>(values[1]);
        }
        finally
        {
            await admin.Use("Neutral.Drop", $"DROP KEYSPACE IF EXISTS {keyspace}").ExecuteCommandAsync();
        }
    }

    readonly record struct Parameters(object?[] Values) : IBindValue
    {
        public object Bind() => Values;
    }
}
