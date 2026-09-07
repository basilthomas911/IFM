using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.ConfigurationDb.StrategyCatalog;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

[Trait("Category", "Unit")]
public sealed class StrategyCatalogContractTests
{
    [Fact]
    public void Hash_is_independent_of_object_order_numeric_format_and_relationship_order()
    {
        var d = Definition(StrategyCatalogKind.Strategy) with
        {
            Families = [Key(StrategyCatalogKind.Family), Key(StrategyCatalogKind.Family)],
            Settings = Json("{\"z\":1.00,\"a\":{\"y\":2,\"x\":3}}")
        };
        var reordered = d with { Families = d.Families.Reverse().ToArray(), Settings = Json("{\"a\":{\"x\":3,\"y\":2.0},\"z\":1e0}") };
        StrategyCatalogValidation.ContentHash(d).Should().Be(StrategyCatalogValidation.ContentHash(reordered));
        StrategyCatalogValidation.ContentHash(d with { Settings = Json("{\"z\":2,\"a\":{\"y\":2,\"x\":3}}") })
            .Should().NotBe(StrategyCatalogValidation.ContentHash(d));
    }

    [Theory]
    [InlineData("{\"x\":1,\"x\":2}")]
    [InlineData("{\"x\":1e100}")]
    [InlineData("{\"x\":0.123456789012345678901234567891234}")]
    public void Ambiguous_or_lossy_json_is_rejected(string json)
    {
        var d = Definition(StrategyCatalogKind.Strategy) with { Settings = Json(json) };
        FluentActions.Invoking(() => StrategyCatalogValidation.Freeze(d)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Freeze_copies_child_arrays_before_async_storage()
    {
        var original = Definition(StrategyCatalogKind.Strategy) with { Families = [Key(StrategyCatalogKind.Family)] };
        var copy = StrategyCatalogValidation.Freeze(original);
        original.Families[0] = Key(StrategyCatalogKind.Family);
        copy.Families[0].Should().NotBe(original.Families[0]);
    }

    [Fact]
    public void New_strategy_names_need_no_strategy_enum_change()
    {
        foreach (var name in new[] { "JadeLizard", "DoubleCalendar", "MeanReversionFuture", "UninventedStrategy" })
            StrategyCatalogValidation.Freeze(Definition(StrategyCatalogKind.Strategy) with { Code = name }).Code.Should().Be(name);
    }

    [Theory]
    [InlineData(TimeFrameType.Daily)] [InlineData(TimeFrameType.Weekly)] [InlineData(TimeFrameType.Monthly)]
    public void Deployments_accept_only_the_supported_trigger_horizons(TimeFrameType horizon)
    {
        StrategyCatalogValidation.Freeze(Definition(StrategyCatalogKind.Deployment) with { Horizon = horizon }).Horizon.Should().Be(horizon);
        FluentActions.Invoking(() => StrategyCatalogValidation.Freeze(Definition(StrategyCatalogKind.Deployment) with { Horizon = TimeFrameType.OneMinute }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Wrong_parent_kind_and_duplicate_relationships_are_rejected()
    {
        FluentActions.Invoking(() => StrategyCatalogValidation.Freeze(Definition(StrategyCatalogKind.Variant) with { Parent = Key(StrategyCatalogKind.Family) }))
            .Should().Throw<ArgumentException>();
        var family = Key(StrategyCatalogKind.Family);
        FluentActions.Invoking(() => StrategyCatalogValidation.Freeze(Definition(StrategyCatalogKind.Strategy) with { Families = [family, family] }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Multi_expiry_structure_is_valid_but_missing_or_cyclic_groups_are_rejected()
    {
        var d = Definition(StrategyCatalogKind.Structure) with
        {
            ExpiryGroups = [new("Near"), new("Far", "Near")],
            Legs = [new("NearCall", "FuturesOption", "Sell", "Call", 1, "Near"), new("FarCall", "FuturesOption", "Buy", "Call", 1, "Far")]
        };
        StrategyCatalogValidation.Freeze(d).Legs.Should().HaveCount(2);
        FluentActions.Invoking(() => StrategyCatalogValidation.Freeze(d with { ExpiryGroups = [new("Near", "Far"), new("Far", "Near")] }))
            .Should().Throw<ArgumentException>().WithMessage("*Cyclic*");
        FluentActions.Invoking(() => StrategyCatalogValidation.Freeze(d with { ExpiryGroups = [new("Near")] }))
            .Should().Throw<ArgumentException>().WithMessage("*missing*");
    }

    [Theory]
    [InlineData("{\"threshold\":0.5,\"enabled\":true}")]
    [InlineData("{\"threshold\":0,\"enabled\":false}")]
    [InlineData("{\"threshold\":1,\"enabled\":true}")]
    public void Parameter_shape_accepts_explicit_boundary_values(string json) =>
        StrategyCatalogValidation.ValidateParameters(Shape(), Json(json));

    [Theory]
    [InlineData("{\"threshold\":1.001,\"enabled\":true}")]
    [InlineData("{\"threshold\":-0.001,\"enabled\":true}")]
    [InlineData("{\"threshold\":\"0.5\",\"enabled\":true}")]
    [InlineData("{\"threshold\":0.5}")]
    [InlineData("{\"threshold\":0.5,\"enabled\":true,\"typo\":1}")]
    [InlineData("{\"threshold\":null,\"enabled\":true}")]
    public void Invalid_parameters_do_not_receive_silent_defaults(string json) =>
        FluentActions.Invoking(() => StrategyCatalogValidation.ValidateParameters(Shape(), Json(json))).Should().Throw<ArgumentException>();

    [Fact]
    public void Unknown_shape_fields_and_incompatible_constraints_are_rejected()
    {
        FluentActions.Invoking(() => StrategyCatalogValidation.ReadShape(Json("{\"Typo\":1}"))).Should().Throw<JsonException>();
        var shape = Shape() with { Minimum = 0 };
        FluentActions.Invoking(() => StrategyCatalogValidation.ValidateParameters(shape, Json("{}"))).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Unsupported_capability_cannot_be_claimed_by_catalog_metadata()
    {
        var registry = new StrategyCatalogCapabilityRegistry([]);
        FluentActions.Invoking(() => registry.Validate(new("builder", "JadeLizard", 1), Definition(StrategyCatalogKind.Structure),
            new Dictionary<CatalogKey, StoredStrategyCatalogDefinition>())).Should().Throw<InvalidOperationException>().WithMessage("*Unsupported*");
    }

    internal static CatalogParameterShape Shape() => new()
    {
        Required = ["threshold", "enabled"],
        Properties = new()
        {
            ["threshold"] = new() { Type = CatalogValueType.Decimal, Minimum = 0, Maximum = 1, Unit = "fraction" },
            ["enabled"] = new() { Type = CatalogValueType.Boolean }
        }
    };
    internal static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();
    internal static CatalogKey Key(StrategyCatalogKind kind) => new(kind, Guid.NewGuid(), 1);
    internal static StrategyCatalogDefinition Definition(StrategyCatalogKind kind) => new()
    {
        Key = Key(kind), Code = "Test-" + Guid.NewGuid().ToString("N"), Name = "Catalog fixture",
        Parent = kind switch
        {
            StrategyCatalogKind.Variant => Key(StrategyCatalogKind.Structure),
            StrategyCatalogKind.ParameterSet => Key(StrategyCatalogKind.ParameterSchema),
            StrategyCatalogKind.Deployment => Key(StrategyCatalogKind.Strategy), _ => null
        },
        Horizon = kind == StrategyCatalogKind.Deployment ? TimeFrameType.Daily : TimeFrameType.None,
        Side = kind == StrategyCatalogKind.Variant ? "Long" : "",
        Bias = kind == StrategyCatalogKind.Variant ? "Bullish" : "",
        PremiumMode = kind == StrategyCatalogKind.Variant ? "None" : ""
    };
}
