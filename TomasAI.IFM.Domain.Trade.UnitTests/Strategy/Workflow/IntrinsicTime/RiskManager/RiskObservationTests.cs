using System.Collections.Immutable;
using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
using TomasAI.IFM.Framework.Serialization;
namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RiskManager;
public sealed class RiskObservationTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public async Task Maximum_quantity_search_has_bounded_complete_explanation_and_honors_cancellation()
    {
        var input=await RiskFixture.Command();var unit=new RiskEvaluator().Calculate(input).UnitRisk!;
        var funding=Enumerable.Range(1,100).Select(q=>input.Funding[0] with {StrategyUnits=q}).ToImmutableArray();
        var quantities=System.Collections.Immutable.ImmutableArray.CreateBuilder<RiskQuantityCheck>();
        var policy=input.Policy with {MaximumUnits=100};var authority=input.SizingAuthority with {AvailableCash=0};
        var allocated=GC.GetAllocatedBytesForCurrentThread();var elapsed=System.Diagnostics.Stopwatch.StartNew();
        var result=RiskSizingModel.Calculate(unit,policy,authority,100,funding,1,default,quantities.Add);elapsed.Stop();
        allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        result.StrategyUnits.Should().Be(0);quantities.Count.Should().Be(100);
        var proof=new RiskExplanation {Limits=authority.Limits,Quantities=quantities.ToImmutable()};
        var bytes=MessagePackBinarySerializer.MeasureContent(proof);bytes.Should().BeLessThan(524288);
        output.WriteLine($"100 quantities: {elapsed.Elapsed.TotalMilliseconds:F3} ms; allocated {allocated} bytes; explanation {bytes} uncompressed bytes.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        var action=()=>RiskSizingModel.Calculate(unit,policy,authority,100,funding,1,cancelled.Token);
        action.Should().Throw<OperationCanceledException>();
    }
    [Fact]
    public void Legacy_workflow_wire_decodes_without_new_resize_or_explanation_fields()
    {
        var value=new TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView();
        var bytes=MessagePackBinarySerializer.SerializeHistoricalContent(value);
        var reader=new MessagePack.MessagePackReader(bytes);reader.ReadArrayHeader().Should().Be(37);
        var buffer=new System.Buffers.ArrayBufferWriter<byte>();var writer=new MessagePack.MessagePackWriter(buffer);writer.WriteArrayHeader(35);
        for(var i=0;i<35;i++)writer.WriteRaw(reader.ReadRaw());writer.Flush();
        var legacy=MessagePackBinarySerializer.Shared.Deserialize<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView>(buffer.WrittenSpan.ToArray());
        legacy.RiskResize.Should().BeNull();legacy.RiskExplanation.Should().BeNull();
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(1000000)]
    public async Task Explanation_proves_the_selected_quantity_and_preserves_result_hash(decimal cash)
    {
        var input=await RiskFixture.Command();
        input=input with { SizingAuthority=input.SizingAuthority with {AvailableCash=cash} };
        input=input with {InputSha256=input.Fingerprint()};
        var result=new RiskEvaluator().Calculate(input);var original=RiskContracts.Hash(result);
        var proof=RiskExplanationModel.Create(input,result);
        proof.Quantities.Should().NotBeEmpty();
        proof.Quantities.Where(x=>x.Units>result.StrategyUnits).Should().OnlyContain(x=>!x.CashFits || !x.LossFits || x.Limits.Any(l=>!l.Fits));
        if(result.StrategyUnits>0)proof.Quantities.Last().Units.Should().Be(result.StrategyUnits);
        else proof.Quantities.Last().Units.Should().Be(1);
        var restored=MessagePackBinarySerializer.Shared.Deserialize<RiskExplanation>(MessagePackBinarySerializer.Shared.Serialize(proof));
        restored.Hash().Should().Be(proof.ContentHash);restored.ResultHash.Should().Be(original);
        RiskContracts.Hash(result).Should().Be(original);
    }
    [Fact]
    public void Paging_is_bound_to_risk_scope_date_and_size()
    {
        var date=new DateOnly(2026,9,9);var token=RiskPaging.Encode(1,2,date,25,[1,2,3]);
        RiskPaging.Decode(1,2,date,25,token).Should().Equal(1,2,3);
        var crossFund=()=>RiskPaging.Decode(1,3,date,25,token);crossFund.Should().Throw<ArgumentException>();
        var crossDate=()=>RiskPaging.Decode(1,2,date.AddDays(1),25,token);crossDate.Should().Throw<ArgumentException>();
        var crossSize=()=>RiskPaging.Decode(1,2,date,50,token);crossSize.Should().Throw<ArgumentException>();
        var composition=TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.OrderComposition.OrderCompositionPaging.Encode(1,2,date,25,[1,2,3]);
        var crossDomain=()=>RiskPaging.Decode(1,2,date,25,composition);crossDomain.Should().Throw<ArgumentException>();
    }
}
