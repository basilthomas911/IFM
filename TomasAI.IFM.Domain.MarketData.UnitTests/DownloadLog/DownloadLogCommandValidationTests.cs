using System.Reflection;
using TomasAI.IFM.Domain.MarketData.DownloadLog.Command.Actor;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Validation;
using Xunit;

namespace TomasAI.IFM.Domain.MarketData.UnitTests.DownloadLog;

public sealed class DownloadLogCommandValidationTests
{
    static List<ValidationError> Validate(InsertMarketDataDownloadLogCommand command)
    {
        var map = (IReadOnlyDictionary<Type, Func<ICommand, List<ValidationError>>>)typeof(DownloadLogCommandActor)
            .GetField("_validationMap", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        return map[typeof(InsertMarketDataDownloadLogCommand)](command);
    }

    [Theory]
    [InlineData("FMP")]
    [InlineData("USTreasury")]
    public void Valid_terminal_evidence_is_admitted(string provider)
    {
        var outcome = DownloadLogContractTests.Outcome(MarketDataDownloadDataset.TreasuryCurve) with { Provider = provider };
        Assert.Empty(Validate(new(outcome)));
    }

    [Fact]
    public void Missing_id_entity_and_payload_errors_are_aggregated_without_throwing()
    {
        var errors = Validate(new InsertMarketDataDownloadLogCommand { EntityId = null!, Outcome = null! });
        Assert.True(errors.Count >= 4);
        Assert.Contains("CommandId", errors[0].ErrorMessage);
        Assert.Contains(errors, error => error.ErrorMessage.Contains("identity"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("Outcome"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("PayloadSha256"));
    }

    [Fact]
    public void Invalid_structured_evidence_collects_partition_time_and_count_errors()
    {
        var command = new InsertMarketDataDownloadLogCommand(DownloadLogContractTests.Outcome());
        var outcome = command.Outcome with { Scope = "C1", RequestedAtUtc = DateTime.SpecifyKind(command.Outcome.RequestedAtUtc, DateTimeKind.Local),
            PersistedRecordCount = -1, ErrorCode = "Unexpected error on a completed download" };
        var errors = Validate(command with { Outcome = outcome });
        Assert.Contains(errors, error => error.ErrorMessage.Contains("Scope"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("RequestedAtUtc"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("PersistedRecordCount"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("ErrorCode"));
    }

    [Fact]
    public void Valid_payload_with_changed_identity_or_hash_is_rejected()
    {
        var command = new InsertMarketDataDownloadLogCommand(DownloadLogContractTests.Outcome());
        var errors = Validate(command with { CommandId = Guid.NewGuid(), EntityId = new(Guid.NewGuid()), PayloadSha256 = "changed" });
        Assert.Contains(errors, error => error.ErrorMessage.Contains("EntityId"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("CommandId"));
        Assert.Contains(errors, error => error.ErrorMessage.Contains("PayloadSha256"));
    }
}
