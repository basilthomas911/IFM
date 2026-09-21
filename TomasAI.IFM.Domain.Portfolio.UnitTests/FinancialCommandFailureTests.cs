using TomasAI.IFM.Application.Storage.CommandAudit;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests;

public sealed class FinancialCommandFailureTests
{
    [Fact]
    public async Task Audit_payload_conflict_preserves_request_mismatch_contract()
    {
        var result = await new CommandAuditPayloadConflictException(Guid.NewGuid()).FinancialCommandFailure();
        Assert.False(result.Success);
        Assert.Equal(FinancialReasons.RequestMismatch, result.ErrorCode);
    }

    [Fact]
    public async Task Unrelated_persistence_failure_is_not_reported_as_request_mismatch()
    {
        var result = await new IOException("database unavailable").FinancialCommandFailure();
        Assert.False(result.Success);
        Assert.Equal(FinancialReasons.PersistenceFailed, result.ErrorCode);
    }
}
