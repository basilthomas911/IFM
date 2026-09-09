using FluentAssertions;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Financial;

[Trait("Category","PortfolioFinancial")]
public sealed class FinancialAuthorityPreparationTests
{
    static readonly DateTime Now=new(2026,9,8,15,0,0,DateTimeKind.Utc);
    [Fact]
    public void Preparation_pins_exact_sources_and_distinguishes_per_trade_from_aggregate_loss()
    {
        var f=Fixture();var prepared=f.Prepare();var fund=prepared.Draft.Book!.Funds.Single();
        fund.CanSpend.Should().BeTrue();var deployment=fund.Deployments.Single();
        deployment.MaximumRiskPerTrade.Should().Be(500);
        deployment.Limits.Single(x=>x.Measure==CapacityMeasure.LossCharge).Maximum.Should().Be(4000);
        deployment.Reference.DeploymentKey.Should().Be(f.Key);deployment.Reference.AuthorityEpoch.Should().Be(7);
        deployment.Reference.AssignmentVersion.Should().Be(3);deployment.Reference.EnvelopeVersion.Should().Be(4);
        deployment.Reference.ValidUntilUtc.Should().Be(Now.AddMinutes(5));
        fund.PortfolioStreamVersion.Should().Be(3);fund.FundStreamVersion.Should().Be(2);
        fund.Limits.Where(x=>x.ScopeKind==CapacityScopeKind.Underlying).Should().OnlyContain(x=>x.ScopeKey=="U1:ES|GLBX|USD");
        f.Financial.Book.Funds.Single().CanSpend.Should().BeFalse("preparation must not mutate saved authority");
    }
    [Theory]
    [InlineData("unqualified")][InlineData("disabled")][InlineData("catalog")][InlineData("product")][InlineData("expired")]
    public void Missing_or_disabled_inputs_cannot_enable_new_spending(string missing)
    {
        var f=Fixture();var financial=missing=="unqualified"?f.Financial with { Book=f.Financial.Book with { MigrationQualified=false } }:f.Financial;
        var catalog=missing=="catalog"?new Dictionary<CatalogKey,StoredStrategyCatalogDefinition>():f.Catalog;
        if(missing=="product") catalog[f.Key]=catalog[f.Key] with { Definition=catalog[f.Key].Definition with { Products=[new(2,"NQ","GLBX","USD")] } };
        var result=FinancialAuthorityPreparationModel.Create(financial,f.Portfolio,f.Funds,f.Policy,catalog,missing!="disabled",missing=="expired"?Now.AddHours(2):Now);
        result.Draft.Book!.Funds.Should().OnlyContain(x=>!x.CanSpend && x.Deployments.Length==0);
    }
    [Fact]
    public void Caller_cannot_raise_the_per_trade_cap_after_preparation()
    {
        var f=Fixture();var book=f.Prepare().Draft.Book!;var fund=book.Funds.Single();
        book=book with { Funds=[fund with { Deployments=[fund.Deployments.Single() with { MaximumRiskPerTrade=501 }] }] };
        FluentActions.Invoking(()=>FinancialAuthorityModel.Validate(book,f.Portfolio,f.Funds,new Dictionary<int,PortfolioFinancialPolicyAggregate> { [9]=f.Policy },Now))
            .Should().Throw<FinancialOperationException>().WithMessage("*per-trade*");
    }
    static Inputs Fixture()
    {
        var key=new CatalogKey(StrategyCatalogKind.Deployment,Guid.NewGuid(),2);var family=new TradeStrategyFamilyReference(0,0) { CatalogDeployment=key };
        var portfolio=new PortfolioAggregate();var fund=new PortfolioFundAggregate();var policy=new PortfolioFinancialPolicyAggregate();
        var envelope=new FundRiskEnvelopeReadModel { PortfolioId=1,PortfolioVersion=1,FundId=2,FundMandateVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=4,
            CapacityState=FundCapacityState.Available,AllocatedCapital=10000,AvailableCapital=9000,MaximumRiskPerTrade=800,MaximumAggregateRisk=4000,
            RemainingLossBudget=5000,MaximumMargin=5000,MaximumGrossNotional=100000,MaximumContracts=20,MaximumOpenPositions=10,
            MaximumAbsoluteDelta=100,EffectiveFromUtc=Now.AddHours(-1),ExpiresAtUtc=Now.AddHours(1),SourcePolicyId=9,SourcePolicyVersion=1 };
        portfolio.Replay([new PortfolioCreated(Guid.NewGuid(),Guid.NewGuid(),1,Now,"fixture",new() { PortfolioId=1,PortfolioVersion=1,OperatingState=PortfolioOperatingState.Active,
            BrokerAccountRefs=["DEV"],ActivePolicyId=9,ActivePolicyVersion=1,EffectiveFromUtc=Now.AddDays(-1) }),
            new FundAddedToPortfolio(Guid.NewGuid(),Guid.NewGuid(),2,Now,"fixture",new(1,2)),new FundRiskEnvelopeDelegated(Guid.NewGuid(),Guid.NewGuid(),3,Now,"fixture",envelope)]);
        fund.Replay([new FundMandateCreated(Guid.NewGuid(),Guid.NewGuid(),1,Now,"fixture",new() { PortfolioId=1,FundId=2,FundMandateVersion=1,Name="Test Fund",
            OperatingState=FundOperatingState.Active,UnderlyingUniverse=["ES"],PermittedTradeStrategyFamilies=[family],EffectiveFromUtc=Now.AddDays(-1) }),
            new FundTradeTemplateAssigned(Guid.NewGuid(),Guid.NewGuid(),2,Now,"fixture",new() { PortfolioId=1,PortfolioVersion=1,FundId=2,FundMandateVersion=1,
                AssignmentVersion=3,TradeTemplateId=key.Id,TradeTemplateVersion=key.Version,TradeStrategyFamily=family,Enabled=true,UnderlyingUniverse=["ES"],EffectiveFromUtc=Now.AddDays(-1) })]);
        policy.Replay([new PortfolioFinancialPolicyCreated(Guid.NewGuid(),Guid.NewGuid(),1,Now,"fixture",new() { PortfolioId=1,PolicyId=9,PolicyVersion=1,
            OperatingState=PortfolioFinancialPolicyState.Active,CapitalBase=100000,MaximumDeployableCapital=90000,MaximumRiskPerTrade=1000,MaximumAggregateRisk=10000,
            MaximumMargin=10000,MaximumGrossNotional=1000000,MaximumOpenPositions=100,EffectiveFromUtc=Now.AddDays(-1),TradeFamilyLimits=[new() {
                CatalogDeployment=key,Enabled=true,MaximumRiskPerTrade=500,MaximumAggregateRisk=6000,MaximumMargin=10000,MaximumGrossNotional=1000000,MaximumOpenPositions=20 }] },Guid.NewGuid())]);
        var financial=new FinancialAuthorityPreparationSnapshot(new() { BookId=3,PortfolioId=1,AccountingEntityId=Guid.NewGuid(),ExecutionAccountReference="DEV",MigrationQualified=true,
            Funds=[new() { FundId=2 }] },5,7,"NeedsRefresh",new Dictionary<int,decimal> { [2]=9000 });
        var definition=new StoredStrategyCatalogDefinition(new() { Key=key,Code="DEFAULT",Name="Futures",Products=[new(1,"ES","GLBX","USD")] },new('A',64),CatalogLifecycleStatus.Published,Now,"fixture",Now.AddDays(-1),"fixture",null,null);
        return new(financial,portfolio,new() { [2]=fund },policy,new() { [key]=definition },key);
    }
    sealed record Inputs(FinancialAuthorityPreparationSnapshot Financial,PortfolioAggregate Portfolio,Dictionary<int,PortfolioFundAggregate> Funds,
        PortfolioFinancialPolicyAggregate Policy,Dictionary<CatalogKey,StoredStrategyCatalogDefinition> Catalog,CatalogKey Key)
    {
        public FinancialAuthorityDraft Prepare()=>FinancialAuthorityPreparationModel.Create(Financial,Portfolio,Funds,Policy,Catalog,true,Now);
    }
}
