using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public sealed class ParameterValueSourceTests
{
    [Fact]
    public void ArraySource_ReportsCountWithoutBindingValues()
    {
        var bindCount = 0;
        var source = new ParameterValueSource<CountingBindValue>(
        [
            new(1, () => bindCount++),
            new(2, () => bindCount++),
            new(3, () => bindCount++)
        ]);

        source.Count.Should().Be(3);
        bindCount.Should().Be(0);

        var values = source.Read().ToArray();

        bindCount.Should().Be(3);
        values.Should().HaveCount(3);
        values.Select(value => ((object?[])value)[0]).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void IteratorSource_BindsIncrementallyAndEnumeratesOnce()
    {
        var produced = 0;
        var bound = 0;
        var source = new ParameterValueSource<CountingBindValue>(CreateValues());

        source.Count.Should().BeNull();
        source.Read().Take(2).Should().HaveCount(2);
        produced.Should().Be(2);
        bound.Should().Be(2);

        IEnumerable<CountingBindValue> CreateValues()
        {
            for (var index = 0; index < 100; index++)
            {
                produced++;
                yield return new CountingBindValue(index, () => bound++);
            }
        }
    }

    [Fact]
    public void IndexedSource_BindsOnlyClaimedIndexesOnce()
    {
        var bindCounts = new int[4];
        CountingBindValue[] values =
        [
            new(10, () => bindCounts[0]++),
            new(20, () => bindCounts[1]++),
            new(30, () => bindCounts[2]++),
            new(40, () => bindCounts[3]++)
        ];
        var source = new IndexedParameterValueSource<CountingBindValue>(values);

        source.Count.Should().Be(4);
        ((object?[])source.ReadAt(3))[0].Should().Be(40);
        ((object?[])source.ReadAt(1))[0].Should().Be(20);

        bindCounts.Should().Equal(0, 1, 0, 1);
    }

    [Fact]
    public void BindValueArray_SelectsIndexedRepositoryOverload()
    {
        var context = Substitute.For<IObjectRepositoryContext>();
        CountingBindValue[] values = [new(1, static () => { })];

        context.SetParameters(values);

        var call = context.ReceivedCalls().Should().ContainSingle().Subject;
        call.GetMethodInfo().GetParameters().Should().ContainSingle()
            .Which.ParameterType.Should().Be(typeof(CountingBindValue[]));
    }

    readonly record struct CountingBindValue(int Value, Action OnBind) : IBindValue
    {
        public object Bind()
        {
            OnBind();
            return new object?[] { Value };
        }
    }
}
