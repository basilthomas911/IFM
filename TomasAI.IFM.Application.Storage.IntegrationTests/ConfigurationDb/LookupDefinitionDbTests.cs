using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Collection(MarketConditionConfigurationDbCollection.Name)]
[Trait("Category", "Integration")]
public sealed class LookupDefinitionDbTests(MarketConditionConfigurationDbFixture fixture)
{
    [Fact]
    public async Task Query_returns_exact_seed_groups_in_display_order_with_generated_ids()
    {
        foreach (var (group, count) in new[] { (LookupDefinitionGroups.AssetTypes, 2), (LookupDefinitionGroups.Directions, 3), (LookupDefinitionGroups.MarketConditions, 7) })
        {
            var rows = await fixture.Context.GetLookupDefinitionsAsync(group);
            rows.Should().HaveCount(count);
            rows.Should().OnlyContain(x => x.GroupName == group && x.Id > 0 && x.IsEnabled && x.CreatedUtc.Kind == DateTimeKind.Utc);
            rows.Select(x => x.DisplayOrder).Should().BeInAscendingOrder();
        }
        (await fixture.Context.GetLookupDefinitionsAsync("OtherGroup")).Should().BeEmpty();
        await FluentActions.Invoking(() => fixture.Context.GetLookupDefinitionsAsync("AssetTypes'; DROP TABLE x;--")).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Repeated_seed_preserves_configured_labels_disabled_flags_and_identity()
    {
        var context = fixture.Context;
        await using var connection = context.CreateConnection().As<NpgsqlConnection>(context.ConnectionString);
        await connection.OpenAsync(); await using var tx = await connection.BeginTransactionAsync();
        var original = (await context.GetLookupDefinitionsAsync(LookupDefinitionGroups.AssetTypes)).Single(x => x.InternalValue == "Futures");
        await using (var change = new NpgsqlCommand("UPDATE reference_configuration.lookup_definition SET display_name='Configured label',is_enabled=false WHERE group_name='AssetTypes' AND internal_value='Futures'", connection, tx))
            await change.ExecuteNonQueryAsync();
        await using (var seed = new NpgsqlCommand(LookupDefinitionSchemaSql.Create, connection, tx)) await seed.ExecuteNonQueryAsync();
        await using (var query = new NpgsqlCommand("SELECT id,display_name,is_enabled FROM reference_configuration.lookup_definition WHERE group_name='AssetTypes' AND internal_value='Futures'", connection, tx))
        await using (var reader = await query.ExecuteReaderAsync())
        {
            (await reader.ReadAsync()).Should().BeTrue();
            reader.GetInt32(0).Should().Be(original.Id); reader.GetString(1).Should().Be("Configured label"); reader.GetBoolean(2).Should().BeFalse();
        }
        await tx.RollbackAsync();
    }
}
