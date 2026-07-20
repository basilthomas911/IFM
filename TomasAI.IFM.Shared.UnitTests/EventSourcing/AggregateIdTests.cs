using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.UnitTests.EventSourcing;

public class AggregateIdTests
{
    [Fact]
    public void ToHashCodeOk()
    {
        // given...
        var aggregateId1 = new BoundedContextId<string>(BoundedContextName.FundBoundedContext, "Test123");
        var aggregateId2 = new BoundedContextId<string>(BoundedContextName.FundTransactionBoundedContext, "Test456");

        // when..
        var hashCode1 = BoundedContextId.ToHashCode($"{aggregateId1}");
        var hashCode2 = BoundedContextId.ToHashCode($"{aggregateId2}");

        // then...
        hashCode1.Should().NotBeNull();
        hashCode1.Length.Should().Be(64);
        hashCode2.Should().NotBeNull();
        hashCode2.Length.Should().Be(64);
    }

    [Fact]
    public void ToHashCodeNullAggregateIdParameter()
    {
        // given...
        string aggregateId = default;

        // when..
        Action whenAction = () => BoundedContextId.ToHashCode(aggregateId); ;

        // then...
        whenAction.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToHashCodeEmptyAggregateIdParameter()
    {
        // given...
        string aggregateId = "";

        // when..
        Action whenAction = () => BoundedContextId.ToHashCode(aggregateId); ;

        // then...
        whenAction.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToHashCodeBlankAggregateIdParameter()
    {
        // given...
        string aggregateId = "     ";

        // when..
        Action whenAction = () => BoundedContextId.ToHashCode(aggregateId); ;

        // then...
        whenAction.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ToHashCodeDuplicateOk()
    {
        // given...
        var aggregateId = new BoundedContextId<string>(BoundedContextName.FundBoundedContext, "Test123");

        // when..
        var hashCode = BoundedContextId.ToHashCode($"{aggregateId}");
        var dupHashCode = BoundedContextId.ToHashCode($"{aggregateId}");

        // then...
        hashCode.Should().NotBeNull();
        dupHashCode.Should().NotBeNull();
        hashCode.Length.Should().Be(64);
        dupHashCode.Length.Should().Be(64);
        hashCode.Should().Equals(dupHashCode);
    }

}
