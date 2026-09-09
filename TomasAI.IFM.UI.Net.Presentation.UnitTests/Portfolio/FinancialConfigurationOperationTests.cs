using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Portfolio;

public sealed class FinancialConfigurationOperationTests
{
    [Theory]
    [InlineData(FinancialReadStatus.Unknown)]
    [InlineData(FinancialReadStatus.Unavailable)]
    [InlineData(FinancialReadStatus.Found)]
    public async Task An_unusable_receipt_response_never_authorizes_resending(FinancialReadStatus status)
    {
        var request=Request();var store=new PendingFinancialConfigurationStore(DirectoryPath());
        await store.SaveAsync(new(request,PendingFinancialPhase.OutcomeUnknown,"uncertain"));
        var api=Substitute.For<IPortfolioFinancialApi>();
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ServiceResult<FinancialRead<FinancialOperationOutcome>>>(
                new ServiceOk<FinancialRead<FinancialOperationOutcome>>(new(status,null,1,DateTime.UtcNow))));
        if(status==FinancialReadStatus.Found)
            await FluentActions.Awaiting(()=>new FinancialConfigurationOperation(api,store).RecoverAsync(request.OperationId)).Should().ThrowAsync<InvalidDataException>();
        else
            (await new FinancialConfigurationOperation(api,store).RecoverAsync(request.OperationId)).Phase.Should().Be(PendingFinancialPhase.OutcomeUnknown);
        await api.DidNotReceive().ConfigureAsync(Arg.Any<ConfigureLedgerCommand>(),Arg.Any<CancellationToken>());
    }
    [Fact]
    public async Task Lost_reply_survives_new_viewmodel_and_file_store_without_a_second_post()
    {
        var request=Request();var directory=DirectoryPath();var store=new PendingFinancialConfigurationStore(directory);
        var api=Substitute.For<IPortfolioFinancialApi>();
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Read(null,DateTime.UtcNow)));
        api.ConfigureAsync(Arg.Any<ConfigureLedgerCommand>(),Arg.Any<CancellationToken>())
            .Returns(_=>ValueTask.FromException<ServiceResult<GuidResult>>(new IOException("Lost acknowledgement")));
        var uncertain=await new FinancialConfigurationOperation(api,store).SubmitAsync(request);
        uncertain.Phase.Should().Be(PendingFinancialPhase.OutcomeUnknown);
        var restartedStore=new PendingFinancialConfigurationStore(directory);
        (await restartedStore.ListAsync(1)).Single().Request.OperationId.Should().Be(request.OperationId);
        var completed=Completion(request);
        var recoveredApi=Substitute.For<IPortfolioFinancialApi>();
        recoveredApi.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Read(completed,DateTime.UtcNow)));
        var recovered=await new FinancialConfigurationOperation(recoveredApi,restartedStore).RecoverAsync(request.OperationId);
        recovered.Phase.Should().Be(PendingFinancialPhase.Committed);recovered.Completion!.Id.Should().Be(completed.Id);
        await recoveredApi.DidNotReceive().ConfigureAsync(Arg.Any<ConfigureLedgerCommand>(),Arg.Any<CancellationToken>());
        var late=await restartedStore.SaveAsync(uncertain);
        late.Phase.Should().Be(PendingFinancialPhase.Committed);
    }

    [Fact]
    public async Task Changed_payload_cannot_reuse_a_persisted_operation_identity()
    {
        var request=Request();var store=new PendingFinancialConfigurationStore(DirectoryPath());
        await store.SaveAsync(new(request,PendingFinancialPhase.Prepared,"prepared"));
        var changed=request with { Body=request.Body with { Reason="changed" } };
        changed=changed with { InputSha256=FinancialCanonicalHash.Request(changed) };
        await FluentActions.Awaiting(()=>store.SaveAsync(new(changed,PendingFinancialPhase.Prepared,"changed")))
            .Should().ThrowAsync<InvalidOperationException>();
        (await store.LoadAsync(request.OperationId))!.Request.Body.Reason.Should().Be("reconcile");
    }

    [Fact]
    public async Task Expired_request_is_not_resent_after_fenced_no_receipt_result()
    {
        var request=Request();var store=new PendingFinancialConfigurationStore(DirectoryPath());
        await store.SaveAsync(new(request,PendingFinancialPhase.OutcomeUnknown,"lost reply"));
        var api=Substitute.For<IPortfolioFinancialApi>();
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Read(null,request.ExpiresAtUtc.AddSeconds(1))));
        var result=await new FinancialConfigurationOperation(api,store).RecoverAsync(request.OperationId);
        result.Phase.Should().Be(PendingFinancialPhase.ExpiredWithoutPosting);
        await api.DidNotReceive().ConfigureAsync(Arg.Any<ConfigureLedgerCommand>(),Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_different_operations_receipt_cannot_be_displayed_as_committed()
    {
        var request=Request();var store=new PendingFinancialConfigurationStore(DirectoryPath());
        await store.SaveAsync(new(request,PendingFinancialPhase.Prepared,"prepared"));
        var api=Substitute.For<IPortfolioFinancialApi>();
        api.GetPostingReceiptAsync(Arg.Any<FinancialReadScope>(),Arg.Any<GetPostingReceiptRequest>(),Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Read(Completion(Request()),DateTime.UtcNow)));
        await FluentActions.Awaiting(()=>new FinancialConfigurationOperation(api,store).RecoverAsync(request.OperationId)).Should().ThrowAsync<InvalidDataException>();
        (await store.LoadAsync(request.OperationId))!.Phase.Should().NotBe(PendingFinancialPhase.Committed);
    }

    [Fact]
    public async Task Two_file_store_instances_cannot_overwrite_same_identity_with_different_configuration()
    {
        var request=Request();var directory=DirectoryPath();var changed=request with { Body=request.Body with { Reason="competing" } };
        changed=changed with { InputSha256=FinancialCanonicalHash.Request(changed) };
        async Task<bool> Save(ConfigureLedgerCommand value)
        {
            try { await new PendingFinancialConfigurationStore(directory).SaveAsync(new(value,PendingFinancialPhase.Prepared,"prepared"));return true; }
            catch(InvalidOperationException) { return false; }
        }
        var results=await Task.WhenAll(Save(request),Save(changed));results.Count(x=>x).Should().Be(1);
    }
    static string DirectoryPath()=>Path.Combine(AppContext.BaseDirectory,"TestResults","FinancialConfigurationPending",Guid.NewGuid().ToString("N"));
    static ConfigureLedgerCommand Request()
    {
        var id=Guid.NewGuid();var now=DateTime.UtcNow;
        var request=new ConfigureLedgerCommand { CommandId=id,OperationId=id,PortfolioId=1,EntityId=new(1),
            RequestedAtUtc=now,ExpiresAtUtc=now.AddMinutes(1),Access=new("operator",["LedgerRead","LedgerConfigure"],[1]),
            Body=new() { BookId=1,Action=LedgerConfigurationAction.Reconcile,SourceCut="test:1",Reason="reconcile" } };
        return request with { InputSha256=FinancialCanonicalHash.Request(request) };
    }
    static LedgerConfigurationCompletedEvent Completion(ConfigureLedgerCommand request)=>new()
    {
        Id=Guid.NewGuid(),CommandId=request.CommandId,OperationId=request.OperationId,PortfolioId=request.PortfolioId,InputHash=request.InputSha256,
        Receipt=new(request.OperationId,request.Body.BookId,request.Body.Action,1,DateTime.UtcNow,request.OperationId,"Active")
    };
    static ServiceResult<FinancialRead<FinancialOperationOutcome>> Read(LedgerConfigurationCompletedEvent? result,DateTime at)
        =>new ServiceOk<FinancialRead<FinancialOperationOutcome>>(new(result is null?FinancialReadStatus.NotFound:FinancialReadStatus.Found,
            result is null?null:new() { Configuration=result },1,at));
}
