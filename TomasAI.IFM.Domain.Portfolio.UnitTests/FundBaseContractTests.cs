using FluentAssertions;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Query;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests;

public sealed class FundBaseContractTests
{
    static readonly DateOnly Day = new(2026, 11, 10);
    static FuturesContractV3ReadModel Contract(string symbol, DateOnly expiry, bool current = false) => new() {
        Symbol = symbol, ContractId = $"{symbol}{expiry:yyyyMMdd}", LastTradeDate = expiry, OnTheRun = current, Rollover = current };
    static InstrumentDefinitionSelection Definition(string symbol, DateOnly expiry) => new() {
        Root = symbol, InstrumentClass = "F", Dataset = "GLBX.MDP3", InstrumentId = 1,
        ExpirationUtc = new DateTimeOffset(expiry.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        DefinitionDigest = new('a',64), RawDefinitionReference = "instrument_definition:test" };

    [Theory]
    [InlineData("Equities", "ES", 0)] [InlineData("Futures", "", 0)]
    [InlineData("Futures", " ES", 0)] [InlineData("Futures", "ES", -1)] [InlineData("Futures", "ES", 121)]
    public void Invalid_rules_cannot_be_saved(string asset, string symbol, int cycles) =>
        new FundBaseContractRule { BaseAsset = asset, Symbol = symbol, ForwardCycles = cycles }.Validate().Should().NotBeEmpty();

    [Fact]
    public void Current_plus_N_uses_listed_cycles_and_provider_provenance()
    {
        DateOnly december = new(2026,12,18), march = new(2027,3,19), june = new(2027,6,18), distant = new(2031,12,19);
        var rows = new[] { Contract("ES",distant), Contract("ES",june), Contract("ES",march), Contract("ES",december,true), Contract("VX",december,true) };
        var definitions = new[] { Definition("ES",december), Definition("ES",march), Definition("ES",june) };
        var resolved = FundBaseContractQueryService.Resolve(new() { Symbol="ES", ForwardCycles=1 }, rows, definitions, Day);
        resolved.Select(x=>x.ContractId).Should().Equal("ES20261218","ES20270319");
        resolved.Should().OnlyContain(x=>x.Dataset=="GLBX.MDP3" && x.RawDefinitionReference!="");
    }

    [Fact]
    public void Rollover_follows_current_flag_and_excludes_expired_deleted_and_option_definitions()
    {
        DateOnly first = new(2026,11,18), second = new(2026,12,16), third = new(2027,1,20);
        var rows = new[] { Contract("VX",first), Contract("VX",second,true), Contract("VX",third) };
        var definitions = new[] { Definition("VX",first), Definition("VX",second), Definition("VX",third) with { Deleted=true } };
        FundBaseContractQueryService.Resolve(new(){Symbol="VX",ForwardCycles=2},rows,definitions,Day)
            .Select(x=>x.ContractId).Should().Equal("VX20261216");
        FundBaseContractQueryService.Resolve(new(){Symbol="VX"},rows,definitions,new(2027,2,1)).Should().BeEmpty();
        FundBaseContractQueryService.Resolve(new(){Symbol="VX"},rows,definitions.Select(x=>x with{InstrumentClass="C"}),Day).Should().BeEmpty();
    }

    [Fact]
    public void Configuration_preserves_mandate_bindings_and_rejects_unrelated_changes_or_stale_revision()
    {
        var now = new DateTime(2026,11,10,10,0,0,DateTimeKind.Utc);
        var original = new FundMandateReadModel { PortfolioId=1,FundId=2,FundCode="ES",Name="Test",FundMandateVersion=1,
            SchemaVersion=3,TradingYear=2026,OperatingState=TomasAI.IFM.Domain.Portfolio.Shared.Contracts.FundOperatingState.Draft,
            EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="test",DecisionHorizon="Daily",Objective="Test",
            UnderlyingUniverse=["ES"],EligibleAssetTypes=["Futures"] };
        var state = new TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioFundAggregate();
        var create = state.Create(Guid.NewGuid(),original,now,"test");
        var configured = original with { SchemaVersion=4,BaseContracts=[new(){Symbol="ES",ForwardCycles=2}] };
        var saved = state.ConfigureBaseContracts(Guid.NewGuid(),1,configured,now,"test");
        state.Current!.FundMandateVersion.Should().Be(1);
        state.Current.PermittedTradeStrategyFamilies.Should().BeEquivalentTo(original.PermittedTradeStrategyFamilies);
        var restored = new TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioFundAggregate();
        restored.Replay([create,saved]);
        restored.Current.Should().BeEquivalentTo(state.Current);
        Action bad = ()=>state.ConfigureBaseContracts(Guid.NewGuid(),2,configured with{Name="Unauthorized rename"},now,"test");
        bad.Should().Throw<ArgumentException>();
        Action stale = ()=>state.ConfigureBaseContracts(Guid.NewGuid(),1,configured,now,"test");
        stale.Should().Throw<InvalidOperationException>();
        state.Revision.Should().Be(2);
    }

    [Fact]
    public void Missing_current_contract_never_defaults_to_a_distant_future()
    {
        DateOnly future = new(2031,12,19);
        FundBaseContractQueryService.Resolve(new(){Symbol="ES"},[Contract("ES",future)],
            [Definition("ES",future)],Day).Should().BeEmpty();
    }

    [Fact]
    public void Rules_round_trip_without_aliasing_and_old_wire_defaults_remain_empty()
    {
        var fund = new FundMandateReadModel { BaseContracts=[new(){Symbol="ES",ForwardCycles=2}] };
        var copy = fund.DefensiveCopy();
        fund.BaseContracts[0] = new(){Symbol="VX"};
        copy.BaseContracts[0].Symbol.Should().Be("ES");
        MessagePackSerializer.Deserialize<FundMandateReadModel>(MessagePackSerializer.Serialize(copy)).BaseContracts.Should().BeEquivalentTo(copy.BaseContracts);
        var old = new byte[] { 0x90 }; // Historic empty/default MessagePack DTO has no appended keys.
        MessagePackSerializer.Deserialize<FundMandateReadModel>(old).BaseContracts.Should().BeEmpty();
    }
}
