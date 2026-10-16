using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using TomasAI.IFM.Application.Storage.ConfigurationDb;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ConfigurationDb;

public sealed class TypedParameterMappingTests
{
    public static IEnumerable<object[]> Variants() => typeof(ParameterSetValue).GetCustomAttributes(typeof(UnionAttribute), false)
        .Cast<UnionAttribute>().Select(x=>new object[]{x.SubType});

    [Theory][MemberData(nameof(Variants))]
    public void All_eight_concrete_contracts_round_trip_through_typed_relational_rows(Type variant)
    {
        var concrete=Activator.CreateInstance(variant.GetProperty("Value")!.PropertyType)!;
        var value=((ParameterSetValue)Activator.CreateInstance(variant,concrete)!).WithIdentity(Guid.NewGuid(),3);
        var rows=ParameterValueMapping.Write(value);
        var restored=ParameterValueMapping.Read(value.ComponentCode,rows);
        Assert.Equal(ParameterValueCodec.Encode(value),ParameterValueCodec.Encode(restored));
        Assert.All(rows.SelectMany(x=>x.Values),x=>Assert.False(x is byte[],"Relational values must not be encoded documents"));
    }

    [Fact]
    public void Ordered_children_and_exact_decimals_survive_relational_round_trip()
    {
        var value=ParameterSetValue.From(new IronCondorMarketSelectionParameterSet
        {
            ParameterSetId=Guid.NewGuid(),Version=5,Symbols=[
                new(){Symbol="ES",CallSpreadWidth=12345678901234567890.123456789m},
                new(){Symbol="MES",CallSpreadWidth=50.000000000000000000000000001m}]
        });
        var rows=ParameterValueMapping.Write(value);
        var restored=ParameterValueMapping.Read(value.ComponentCode,rows.Reverse().ToArray());
        Assert.Equal(ParameterValueCodec.Encode(value),ParameterValueCodec.Encode(restored));
        var symbols=rows.Where(x=>x.Table.Name.EndsWith("_symbols",StringComparison.Ordinal)).ToArray();
        var bad=rows.Where(x=>x!=symbols[0]).ToArray();
        Assert.Throws<InvalidOperationException>(()=>ParameterValueMapping.Read(value.ComponentCode,bad));
    }

    [Fact]
    public void Sql_identifiers_are_unique_and_within_postgres_limit()
    {
        var tables=ParameterValueMapping.Tables.ToArray();
        Assert.True(tables.Length>8);
        Assert.Equal(tables.Length,tables.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.All(tables,x=>
        {
            Assert.InRange(x.Name.Length,1,63);
            Assert.Equal(x.Columns.Count,x.Columns.Select(c=>c.Name).Distinct(StringComparer.Ordinal).Count());
            Assert.All(x.Columns,c=>Assert.DoesNotContain("json",ParameterValueMapping.SqlType(c.Type)));
        });
    }

    [Fact]
    public void Default_and_empty_immutable_collections_remain_distinct()
    {
        var empty = ParameterSetValue.From(new TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet
            {ParameterSetId=Guid.NewGuid(),Version=1,BiasRows=[]});
        var missing = ParameterSetValue.From(empty.Get<TomasAI.IFM.Domain.MarketData.Shared.OptionChainCache.StrategyOptionChainParameterSet>() with{BiasRows=default});
        Assert.NotEqual(ParameterValueCodec.Hash(empty),ParameterValueCodec.Hash(missing));
        foreach(var value in new[]{empty,missing})
            Assert.Equal(ParameterValueCodec.Encode(value),ParameterValueCodec.Encode(ParameterValueMapping.Read(value.ComponentCode,ParameterValueMapping.Write(value))));
    }
}
