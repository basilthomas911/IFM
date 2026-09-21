using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

public sealed class WeightedAverageClosingBasisTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Partial_then_final_close_allocates_average_entry_cost(int direction)
    {
        OpeningBasisLot[] lots = [new(direction, 6000, 50), new(direction, 6010, 50)];
        var first = WeightedAverageClosingBasis.Allocate(lots, -direction);
        first.AllocatedSignedBasis.Should().Be(direction * 300250m);
        first.RemainingSignedQuantity.Should().Be(direction);
        first.RemainingSignedBasis.Should().Be(direction * 300250m);
        var final = WeightedAverageClosingBasis.Allocate(lots, -direction,
            first.CumulativeClosedQuantity, first.CumulativeAllocatedSignedBasis);
        final.AllocatedSignedBasis.Should().Be(first.AllocatedSignedBasis);
        final.RemainingSignedQuantity.Should().Be(0);
        final.RemainingSignedBasis.Should().Be(0);
    }

    [Fact]
    public void Unequal_fill_quantities_are_weighted_not_averaged_by_fill_count()
    {
        var result = WeightedAverageClosingBasis.Allocate([new(1, 100, 50), new(3, 120, 50)], -1);
        result.AllocatedSignedBasis.Should().Be(5750);
        result.RemainingSignedBasis.Should().Be(17250);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void Fragmented_closes_conserve_all_cents_and_match_combined_close(int direction)
    {
        OpeningBasisLot[] lots = [new(direction, 1, 1), new(2 * direction, 1.01m, 1)];
        var a = WeightedAverageClosingBasis.Allocate(lots, -direction);
        var b = WeightedAverageClosingBasis.Allocate(lots, -direction, a.CumulativeClosedQuantity, a.CumulativeAllocatedSignedBasis);
        var c = WeightedAverageClosingBasis.Allocate(lots, -direction, b.CumulativeClosedQuantity, b.CumulativeAllocatedSignedBasis);
        a.AllocatedSignedBasis.Should().Be(direction * 1.01m);
        b.AllocatedSignedBasis.Should().Be(direction * 1.00m);
        c.AllocatedSignedBasis.Should().Be(direction * 1.01m);
        (a.AllocatedSignedBasis + b.AllocatedSignedBasis + c.AllocatedSignedBasis).Should()
            .Be(WeightedAverageClosingBasis.Allocate(lots, -3 * direction).AllocatedSignedBasis);
        c.RemainingSignedBasis.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(-3, 0, 0)]
    [InlineData(-1, 2, 10000)]
    [InlineData(-1, 1, 4999)]
    [InlineData(-1, -1, 0)]
    [InlineData(0, 0, 0)]
    public void Invalid_direction_overclose_or_consumed_evidence_is_rejected(
        decimal close, decimal consumedQuantity, decimal consumedBasis)
    {
        FluentActions.Invoking(() => WeightedAverageClosingBasis.Allocate([new(2, 100, 50)],
            close, consumedQuantity, consumedBasis)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Mixed_directions_or_multipliers_and_fractional_cent_settlement_are_rejected()
    {
        OpeningBasisLot[][] invalid = [[], [new(1, 100, 50), new(-1, 100, 50)],
            [new(1, 100, 50), new(1, 100, 5)], [new(1, 0, 50)], [new(1, 1.001m, 1)]];
        foreach (var lots in invalid)
            FluentActions.Invoking(() => WeightedAverageClosingBasis.Allocate(lots, -1))
                .Should().Throw<ArgumentException>();
    }
}
