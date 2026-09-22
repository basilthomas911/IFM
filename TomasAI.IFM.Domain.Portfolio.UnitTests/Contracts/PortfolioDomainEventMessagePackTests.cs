using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.Fund.Events;
using TomasAI.IFM.Domain.Portfolio.Shared.FinancialPolicy.Events;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Contracts;

public sealed class PortfolioDomainEventMessagePackTests
{
    [Fact]
    public void Fund_event_round_trips_through_its_concrete_contract()
    {
        var value = new FundManualOrderDeletedEvent(
            Guid.NewGuid(), Guid.NewGuid(), 7, new DateTime(2026, 9, 21, 14, 30, 0, DateTimeKind.Utc), "test", 42)
        {
            AggregateId = "portfolio-fund:10:20",
            CorrelationId = Guid.NewGuid(),
            CausationId = Guid.NewGuid(),
        };

        var restored = MessagePackSerializer.Deserialize<FundManualOrderDeletedEvent>(
            MessagePackSerializer.Serialize(value));

        var deleted = restored.Should().BeOfType<FundManualOrderDeletedEvent>().Subject;
        deleted.OrderId.Should().Be(42);
        deleted.Revision.Should().Be(7);
        deleted.AggregateId.Should().Be("portfolio-fund:10:20");
        deleted.Principal.Should().Be("test");
    }

    [Fact]
    public void Event_contracts_use_permanent_numeric_keys()
    {
        KeyOf<FundManualOrderDeletedEvent>(nameof(FundManualOrderDeletedEvent.Subject)).Should().Be(0);
        KeyOf<FundManualOrderDeletedEvent>(nameof(FundManualOrderDeletedEvent.Id)).Should().Be(1);
        KeyOf<FundManualOrderDeletedEvent>(nameof(FundManualOrderDeletedEvent.OriginatedOnUtc)).Should().Be(13);
        KeyOf<FundManualOrderDeletedEvent>(nameof(FundManualOrderDeletedEvent.OrderId)).Should().Be(14);
    }

    static int KeyOf<T>(string propertyName) =>
        typeof(T).GetProperty(propertyName)!
            .GetCustomAttributes(typeof(KeyAttribute), inherit: false)
            .Cast<KeyAttribute>()
            .Single()
            .IntKey!.Value;
}
