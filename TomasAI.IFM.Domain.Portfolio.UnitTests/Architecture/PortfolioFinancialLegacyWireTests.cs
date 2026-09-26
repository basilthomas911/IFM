using System.Reflection;
using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Architecture;

/// <summary>Guards canonical Financial query and command wire layouts.</summary>
public sealed class PortfolioFinancialLegacyWireTests
{
    public static TheoryData<Type> QueryTypes => new()
    {
        typeof(PrepareFinancialBookQuery),
        typeof(PrepareFinancialAuthorityQuery),
        typeof(GetTrialBalanceQuery),
        typeof(GetReconciliationQuery),
        typeof(GetPostingReceiptQuery),
        typeof(GetJournalQuery),
        typeof(GetFundTransactionsPageQuery),
        typeof(GetFundRiskAuthorizationQuery),
        typeof(GetFinancialPostingConfigurationQuery),
        typeof(GetFinancialLedgerConfigurationQuery),
        typeof(GetAccountBalancesQuery),
        typeof(GetFinancialAdmissionSnapshotQuery),
        typeof(GetCapacityReservationQuery),
        typeof(GetCapacityUsageQuery),
        typeof(GetFundReservationsPageQuery),
    };
    public static TheoryData<Type> CommandTypes => new()
    {
        typeof(ReservePortfolioTradeRiskCommand),
        typeof(PostFundTransactionsCommand),
        typeof(PostFundTransactionCommand),
        typeof(ConsumeCapacityReservationCommand),
        typeof(ChangeCapacityReservationCommand),
        typeof(ConfigureLedgerCommand),
        typeof(SubmitEmulatorOrderCommand),
    };

    [Theory, MemberData(nameof(QueryTypes))]
    public void Query_uses_direct_entity_id_and_round_trips(Type messageType) => Check(messageType, "Subject", "EntityId");

    [Theory, MemberData(nameof(CommandTypes))]
    public void Legacy_command_preserves_its_published_schema(Type messageType) => Check(messageType, "SchemaVersion", "CommandId");

    static void Check(Type messageType, string first, string second)
    {
        var keys = messageType.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(property => (Property: property, Key: property.GetCustomAttribute<KeyAttribute>()?.IntKey))
            .Where(item => item.Key.HasValue)
            .OrderBy(item => item.Key)
            .ToArray();
        keys.Select(item => item.Key!.Value).Should().Equal(Enumerable.Range(0, keys.Length));
        keys[0].Property.Name.Should().Be(first);
        keys[1].Property.Name.Should().Be(second);
        var constructor = messageType.GetConstructors().Single(ctor => ctor.IsDefined(typeof(SerializationConstructorAttribute)));
        constructor.GetParameters().Select(parameter => parameter.ParameterType)
            .Should().Equal(keys.Select(item => item.Property.PropertyType));
        var method = typeof(PortfolioFinancialLegacyWireTests).GetMethod(nameof(RoundTrip), BindingFlags.NonPublic | BindingFlags.Static)!;
        method.MakeGenericMethod(messageType).Invoke(null, null);
    }

    static void RoundTrip<T>() where T : new()
    {
        var bytes = MessagePackSerializer.Serialize(new T());
        var copy = MessagePackSerializer.Deserialize<T>(bytes);
        copy.Should().NotBeNull();
        MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(copy)).Should().NotBeNull();
    }
}
