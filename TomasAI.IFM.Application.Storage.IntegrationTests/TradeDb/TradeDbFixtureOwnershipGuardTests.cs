using System;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

public sealed class TradeDbFixtureOwnershipGuardTests
{
    const string OwnedDatabase="ifm_storagetrade_123456abcdef";
    [Fact]
    public void Actual_fixture_connection_is_the_exact_owned_database_and_port()
        =>Assert.Equal("Host=127.0.0.1;Port=50800;Database="+OwnedDatabase,
            TradeDbFixture.RequireOwnedConnection("Host=127.0.0.1;Port=50800;Database="+OwnedDatabase,OwnedDatabase,50800));
    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=ifm_application_development")]
    [InlineData("Host=127.0.0.1;Port=50800;Database=ifm_application_development")]
    [InlineData("Host=127.0.0.1;Port=50801;Database=ifm_storagetrade_123456abcdef")]
    [InlineData("Host=localhost;Port=50800;Database=ifm_storagetrade_123456abcdef")]
    [InlineData("Host=127.0.0.1;Port=50800;Database=ifm_storagetrade_aaaaaaaaaaaa")]
    public void Original_application_and_other_database_targets_are_refused_before_schema_or_writes(string connection)
        =>Assert.Throws<InvalidOperationException>(()=>TradeDbFixture.RequireOwnedConnection(connection,OwnedDatabase,50800));
}
