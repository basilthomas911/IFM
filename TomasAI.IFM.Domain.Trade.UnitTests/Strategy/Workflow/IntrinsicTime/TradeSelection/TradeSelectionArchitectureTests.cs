using System.Reflection;
using System.Collections;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Function.Actor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Query.Actor;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.TradeSelection;
[Trait("Gate","TS-01"),Trait("Gate","TS-07")]
public sealed class TradeSelectionArchitectureTests
{
    [Fact]
    public async Task Function_and_query_maps_are_frozen_and_cover_exact_contract_sets()
    {
        var function=typeof(TradeSelectionFunctionActor);
        function.BaseType!.Name.Should().StartWith("BaseEventSourceFunctionActor");
        foreach(var name in new[]{"_parseMap","_receiveMap","_eventMap"})
            Map(function,name).GetType().FullName.Should().Contain("Frozen");
        Keys(function,"_parseMap").Should().Equal("Execute");Keys(function,"_receiveMap").Should().Equal(typeof(ExecuteTradeSelectionPipelineCommand));
        var fixture = new TradeSelectionFunctionTests.FunctionFixture(await TradeSelectionFixture.Command());
        var validation = function.GetField("_validationMap", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.actor)!;
        validation.GetType().FullName.Should().Contain("Frozen");
        Keys(typeof(TradeSelectionQueryActor),"_receiveMap").Should().BeEquivalentTo(Keys(typeof(TradeSelectionQueryActor),"_exceptionMap"));
    }
    [Fact]
    public void Foundation_has_no_domain_contract_dependency_cycle()
    {
        var assembly=typeof(TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.CatalogKey).Assembly;
        assembly.GetName().Name.Should().Be("TomasAI.IFM.Domain.Strategy.Contracts.Shared");
        assembly.GetReferencedAssemblies().Should().NotContain(x=>x.Name=="TomasAI.IFM.Domain.Trade.Shared" || x.Name=="TomasAI.IFM.Domain.Portfolio.Shared" || x.Name=="TomasAI.IFM.Domain.Reference.Shared");
        var portfolio=typeof(TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi.IPortfolioQueryApi).Assembly;
        portfolio.GetForwardedTypes().Should().Contain(typeof(TomasAI.IFM.Domain.Portfolio.Shared.Contracts.PortfolioFundStrategySnapshot));
    }
    [Theory]
    [InlineData("portfolio")] [InlineData("fund")] [InlineData("date")] [InlineData("size")] [InlineData("corrupt")]
    public void Paging_tokens_cannot_be_used_outside_the_original_query(string mutation)
    {
        var at=new DateOnly(2026,9,7);var encoded=TradeSelectionPaging.Encode(1,2,at,50,[1,2,3]);
        TradeSelectionPaging.Decode(1,2,at,50,encoded).Should().Equal(1,2,3);
        Action read=()=>TradeSelectionPaging.Decode(mutation=="portfolio"?2:1,mutation=="fund"?3:2,mutation=="date"?at.AddDays(1):at,mutation=="size"?51:50,mutation=="corrupt"?"invalid":encoded);
        read.Should().Throw<Exception>();
    }
    [Theory]
    [InlineData("candidates")] [InlineData("assignments")] [InlineData("definitions")] [InlineData("bytes")]
    public async Task Bound_overflow_is_rejected_before_evaluation(string bound)
    {
        var c=await TradeSelectionFixture.Command();var b=c.SelectionBinding;
        b=bound switch
        {
            "candidates"=>b with {Candidates=Enumerable.Repeat(b.Candidates[0],65).ToArray()},
            "assignments"=>b with {PortfolioSnapshot=b.PortfolioSnapshot with {Assignments=Enumerable.Repeat(b.PortfolioSnapshot.Assignments[0],17).ToArray()}},
            "definitions"=>b with {CatalogDefinitions=Enumerable.Repeat(b.CatalogDefinitions[0],257).ToArray()},
            _=>b with {CatalogDefinitions=[b.CatalogDefinitions[0] with {Description=new string('X',300000)},..b.CatalogDefinitions.Skip(1)]}
        };
        Action validate=()=>TradeSelectionContracts.ValidateBinding(TradeSelectionContracts.Seal(b));
        validate.Should().Throw<TradeSelectionValidationException>().Which.ReasonCode.Should().Be(bound=="bytes"?"TS.CONTRACT.PAYLOAD_SIZE":"TS.CONFIG.CANDIDATE_LIMIT");
    }
    static object Map(Type actor,string name)=>actor.GetField(name,BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
    static object[] Keys(Type actor,string name)=>((IEnumerable)Map(actor,name).GetType().GetProperty("Keys")!.GetValue(Map(actor,name))!).Cast<object>().ToArray();
}
