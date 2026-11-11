using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.ReferencePreparation;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Command;

public sealed class FundBaseContractInitializationScenarios
{
    [Fact]
    public void Given_MES_is_not_initialized_when_its_prepared_defaults_are_saved_then_current_plus_two_cycles_survive_replay()
    {
        var now = new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
        var rule = new FundBaseContractRule { Symbol = "MES", ForwardCycles = 2 };
        var definitions = new[] { new DateOnly(2026, 12, 18), new DateOnly(2027, 3, 19), new DateOnly(2027, 6, 17) }
            .Select((date, index) => new InstrumentDefinitionSelection
            {
                Dataset = "GLBX.MDP3", InstrumentId = (uint)(index + 1), Root = "MES", RawSymbol = $"MES-{date:yyyyMMdd}",
                InstrumentClass = "F", Exchange = "XCME", Currency = "USD", Multiplier = 5, TickSize = .25m,
                ExpirationUtc = new(date.ToDateTime(new TimeOnly(14, 30), DateTimeKind.Utc)), DefinitionTimestampUtc = now,
                DefinitionDigest = "verified", RawDefinitionReference = "published-definition"
            }).ToArray();
        var contracts = FundBaseContractPreparation.Plan(rule, new() { Symbol = "MES", Exchange = "XCME", Currency = "USD", Description = "Micro E-mini S&P 500 Futures" },
            definitions, [], DateOnly.FromDateTime(now.UtcDateTime), now);
        var mandate = new FundMandateReadModel { PortfolioId = 101, FundId = 703, FundCode = "Test", Name = "Fund", FundMandateVersion = 1,
            SchemaVersion = 3, TradingYear = 2026, OperatingState = FundOperatingState.Draft, EffectiveFromUtc = now.UtcDateTime,
            CreatedOnUtc = now.UtcDateTime, CreatedBy = "scenario", DecisionHorizon = "Monthly", Objective = "Verification", UnderlyingUniverse = ["MES"], EligibleAssetTypes = ["Futures"] };
        var original = new PortfolioFundAggregate();
        var created = original.Create(Guid.NewGuid(), mandate, now.UtcDateTime, "scenario");
        var saved = original.ConfigureBaseContracts(Guid.NewGuid(), 1, mandate with { SchemaVersion = 4, BaseContracts = [rule] }, now.UtcDateTime, "scenario");
        var restored = new PortfolioFundAggregate(); restored.Replay([created, saved]);
        var resolved = FundBaseContractQueryService.Resolve(restored.Current!.BaseContracts.Single(), contracts, definitions, DateOnly.FromDateTime(now.UtcDateTime));
        resolved.Select(x => x.ContractId).Should().Equal("MES20261218", "MES20270319", "MES20270617");
        resolved[0].OnTheRun.Should().BeTrue();
        contracts.Should().OnlyContain(x => x.MultiplierValue == 5);
        FundBaseContractPreparation.Plan(rule, new() { Symbol = "MES", Exchange = "XCME", Currency = "USD" }, definitions, contracts,
            DateOnly.FromDateTime(now.UtcDateTime), now).Should().Equal(contracts);
    }
}
