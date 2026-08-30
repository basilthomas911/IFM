using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Persistence;

public sealed class PortfolioProjectionContractVerificationTests
{
    [Fact]
    [Trait("Gate", "PF-08")]
    public void Composition_projection_DTOs_round_trip_as_storage_independent_typed_contracts()
    {
        object[] values =
        [
            new FundOrderProjectionReadModel { PortfolioId=1,FundId=2,OrderId=3,WorkflowId=Guid.NewGuid(),Status="Reserved",CreatedOnUtc=DateTime.UtcNow,CreatedBy="verify",AggregateVersion=1 },
            new FundOrderTradeProjectionReadModel { PortfolioId=1,FundId=2,OrderId=3,TradeId=4,TradeFamily="Futures",InstructionReference="ES",LegOrdinal=1,AggregateVersion=1 },
            new FundCompositionWorkflowProjectionReadModel { WorkflowId=Guid.NewGuid(),PortfolioId=1,FundId=2,OrderId=3,Status="Reserved",UpdatedOnUtc=DateTime.UtcNow,AggregateVersion=1 }
        ];
        foreach (var value in values)
        {
            var bytes = MessagePackSerializer.Serialize(value.GetType(), value);
            MessagePackSerializer.Deserialize(value.GetType(), bytes).Should().BeEquivalentTo(value);
        }
    }
}
