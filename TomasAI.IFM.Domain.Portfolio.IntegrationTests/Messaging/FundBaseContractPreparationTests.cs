using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.ReferencePreparation;
using TomasAI.IFM.Domain.Portfolio.Shared.Preparation;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Messaging;

/// <summary>Exercises preparation against the real securities command envelopes and projected-read boundary.</summary>
public sealed class FundBaseContractPreparationTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
    readonly IReferenceDbContext reference = Substitute.For<IReferenceDbContext>();
    readonly ISecuritiesDbContext securities = Substitute.For<ISecuritiesDbContext>();
    readonly IActorProducer producer = Substitute.For<IActorProducer>();
    readonly Dictionary<string, FuturesContractV3ReadModel> projected = [];
    readonly List<AddFuturesContractCommand> additions = [];
    readonly InstrumentDefinitionSnapshot snapshot = new(Guid.NewGuid(), Now.UtcDateTime, 3, ["GLBX.MDP3"]);
    readonly ValidatePortfolioReferenceCommand request = ValidatePortfolioReferenceCommand.ForOwnerCommand(Guid.NewGuid()) with
    {
        Mandate = new() { BaseContracts = [new() { Symbol = "MES", ForwardCycles = 2 }] }
    };
    readonly FundBaseContractPreparation preparation;

    public FundBaseContractPreparationTests()
    {
        var factory = Substitute.For<IDbContextFactory>();
        factory.ReferenceDb.Returns(reference); factory.SecuritiesDb.Returns(securities);
        var dates = Substitute.For<IValueDateProvider>(); dates.ValueDate.Returns(new DateOnly(2026, 11, 10));
        var supervisor = Substitute.For<IActorSupervisor>();
        supervisor.GetProducer(new ActorMailboxId(ActorType.Function, ValidatePortfolioReferenceCommand.Actor)).Returns(producer);
        preparation = new(factory, supervisor, dates, new Clock());
        reference.GetInstrumentDefinitionSnapshotAsync(Arg.Any<CancellationToken>()).Returns(snapshot);
        reference.IsInstrumentDefinitionSelectionCompleteAsync(snapshot.Id, Arg.Any<CancellationToken>()).Returns(true);
        reference.GetInstrumentDefinitionSymbolsAsync(snapshot.Id, TradeStrategyFamilyType.Futures, Arg.Any<CancellationToken>())
            .Returns([new() { Symbol = "MES", Currency = "USD", Exchange = "XCME", Description = "Micro E-mini S&P 500 Futures" }]);
        var definitions = new[] { Definition(2026, 12, 18), Definition(2027, 3, 19), Definition(2027, 6, 17) };
        reference.GetInstrumentDefinitionSelectionAsync(snapshot.Id, "GLBX.MDP3", "MES", false, "", 200, Arg.Any<CancellationToken>())
            .Returns(definitions.Select((x, i) => new InstrumentDefinitionSelectionRow(i.ToString(System.Globalization.CultureInfo.InvariantCulture), x)).ToArray());
        securities.GetFuturesContractsBySymbolAsync("MES", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult<ICollection<FuturesContractV3ReadModel>>(projected.Values.ToArray()));
        securities.GetFuturesContractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(projected.GetValueOrDefault(call.Arg<string>())));
        producer.RequestAsync<AddFuturesContractCommand, FuturesContractId, GuidResult>(Arg.Any<ActorSubject>(), Arg.Any<AddFuturesContractCommand>(), Arg.Any<FuturesContractId>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<AddFuturesContractCommand>(); additions.Add(command);
                ReferencePayloadCodec.ReadFuture(ReferencePayloadCodec.Write(command.Contract), command.Contract).Should().Be(command.Contract);
                projected.Add(command.Contract.ContractId, command.Contract);
                return new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
            });
    }

    InstrumentDefinitionSelection Definition(int year, int month, int day) => new()
    {
        SnapshotId = snapshot.Id, Dataset = "GLBX.MDP3", Root = "MES", InstrumentClass = "F", Exchange = "XCME", Currency = "USD",
        InstrumentId = (uint)(year * 10000 + month * 100 + day), PublisherId = 1, RawSymbol = $"MES{year}{month}",
        ExpirationUtc = new(year, month, day, 14, 30, 0, TimeSpan.Zero), Multiplier = 5, TickSize = .25m,
        DefinitionTimestampUtc = Now, DefinitionDigest = "verified", RawDefinitionReference = "immutable-row"
    };

    [Fact]
    public async Task Saving_new_MES_uses_three_owning_actor_commands_and_observes_their_projections()
    {
        await preparation.EnsureAsync(request, CancellationToken.None);
        additions.Select(x => x.Contract.ContractId).Should().Equal("MES20261218", "MES20270319", "MES20270617");
        additions.Should().OnlyContain(x => !x.Overwrite && x.PostEvents && x.CommandId != Guid.Empty &&
            x.Subject == new ActorSubject(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, x.Contract.Id.Format()));
        await preparation.EnsureAsync(request, CancellationToken.None);
        await preparation.EnsureAsync(request with { OwnerCommandId = Guid.NewGuid() }, CancellationToken.None);
        additions.Should().HaveCount(3, "repeated Save must reuse initialized contracts without duplicate writes");
    }

    [Fact]
    public async Task A_legacy_MES_selection_uses_one_digest_verified_provider_record_to_restore_its_multiplier()
    {
        var dates = new[] { (2026, 12, 18), (2027, 3, 19), (2027, 6, 17) };
        var selections = new List<InstrumentDefinitionSelectionRow>();
        for (var index = 0; index < dates.Length; index++)
        {
            var (year, month, day) = dates[index];
            var original = Definition(year, month, day);
            var json = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["hd"] = new { rtype = 19, publisher_id = original.PublisherId, instrument_id = original.InstrumentId, ts_event = "1794322800000000000" },
                ["ts_recv"] = "1794322800000000000", ["raw_symbol"] = original.RawSymbol, ["asset"] = "MES", ["instrument_class"] = "F",
                ["currency"] = "USD", ["exchange"] = "XCME", ["unit_of_measure"] = "IPNT", ["unit_of_measure_qty"] = "5000000000",
                ["display_factor"] = "10000000", ["min_price_increment"] = "250000000", ["min_price_increment_amount"] = "12500000",
                ["contract_multiplier"] = int.MaxValue, ["underlying_id"] = 0,
                ["expiration"] = (original.ExpirationUtc!.Value.ToUnixTimeMilliseconds() * 1000000).ToString(System.Globalization.CultureInfo.InvariantCulture)
            });
            var selection = original with { Multiplier = null,
                DefinitionDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))),
                RawDefinitionReference = $"instrument_definition:{snapshot.Id:N}:GLBX.MDP3:{index}:{index}" };
            selections.Add(new InstrumentDefinitionSelectionRow(index.ToString(System.Globalization.CultureInfo.InvariantCulture), selection));
            reference.GetInstrumentDefinitionJsonAsync(snapshot.Id, "GLBX.MDP3", index, index, Arg.Any<CancellationToken>()).Returns(json);
        }
        reference.GetInstrumentDefinitionSelectionAsync(snapshot.Id, "GLBX.MDP3", "MES", false, "", 200, Arg.Any<CancellationToken>()).Returns(selections);
        await preparation.EnsureAsync(request, CancellationToken.None);
        projected.Values.Should().HaveCount(3).And.OnlyContain(x => x.MultiplierValue == 5 && x.Multiplier == "5");
    }

    [Fact]
    public async Task Changed_provider_content_cannot_initialize_a_contract()
    {
        var selection = Definition(2026, 12, 18) with { Multiplier = null, DefinitionDigest = "wrong-digest",
            RawDefinitionReference = $"instrument_definition:{snapshot.Id:N}:GLBX.MDP3:0:0" };
        reference.GetInstrumentDefinitionSelectionAsync(snapshot.Id, "GLBX.MDP3", "MES", false, "", 200, Arg.Any<CancellationToken>()).Returns(new[] { new InstrumentDefinitionSelectionRow("0", selection) });
        reference.GetInstrumentDefinitionJsonAsync(snapshot.Id, "GLBX.MDP3", 0, 0, Arg.Any<CancellationToken>()).Returns("{}");
        await FluentActions.Awaiting(() => preparation.EnsureAsync(request, CancellationToken.None))
            .Should().ThrowAsync<InvalidDataException>().WithMessage("*recorded digest*");
        additions.Should().BeEmpty();
    }

    [Fact]
    public async Task A_rejected_securities_command_prevents_a_successful_preparation()
    {
        producer.RequestAsync<AddFuturesContractCommand, FuturesContractId, GuidResult>(Arg.Any<ActorSubject>(), Arg.Any<AddFuturesContractCommand>(), Arg.Any<FuturesContractId>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<GuidResult>>(new ServiceFailed<GuidResult>(6001, "Rejected by catalog")));
        await FluentActions.Awaiting(() => preparation.EnsureAsync(request, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*6001*Fund defaults were not saved*");
        projected.Should().BeEmpty();
    }

    [Fact]
    public async Task An_acknowledgement_without_a_projection_cannot_report_Save_success()
    {
        securities.GetFuturesContractAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((FuturesContractV3ReadModel?)null);
        await FluentActions.Awaiting(() => preparation.EnsureAsync(request, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*not yet projected*Retry Save*");
    }

    [Fact]
    public async Task An_incomplete_snapshot_never_publishes_a_securities_command()
    {
        reference.IsInstrumentDefinitionSelectionCompleteAsync(snapshot.Id, Arg.Any<CancellationToken>()).Returns(false);
        await FluentActions.Awaiting(() => preparation.EnsureAsync(request, CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();
        additions.Should().BeEmpty();
    }

    [Fact]
    public async Task All_selected_symbols_are_validated_before_any_initialization_is_sent()
    {
        var invalid = request with { Mandate = request.Mandate! with { BaseContracts = [new() { Symbol = "MES", ForwardCycles = 2 }, new() { Symbol = "MISSING" }] } };
        await FluentActions.Awaiting(() => preparation.EnsureAsync(invalid, CancellationToken.None)).Should().ThrowAsync<InvalidOperationException>();
        additions.Should().BeEmpty();
    }

    [Fact]
    public async Task A_lost_reply_after_commit_is_recovered_from_projection_without_a_duplicate_add()
    {
        var first = true;
        producer.RequestAsync<AddFuturesContractCommand, FuturesContractId, GuidResult>(Arg.Any<ActorSubject>(), Arg.Any<AddFuturesContractCommand>(), Arg.Any<FuturesContractId>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<AddFuturesContractCommand>(); additions.Add(command);
                projected.Add(command.Contract.ContractId, command.Contract);
                if (first) { first = false; throw new TimeoutException("Reply lost after commit"); }
                return new ValueTask<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new(command.CommandId)));
            });
        await FluentActions.Awaiting(() => preparation.EnsureAsync(request, CancellationToken.None)).Should().ThrowAsync<TimeoutException>();
        await preparation.EnsureAsync(request, CancellationToken.None);
        additions.Should().HaveCount(3);
        additions.Select(x => x.Contract.ContractId).Should().OnlyHaveUniqueItems();
        projected.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cancellation_stops_initialization_without_sending_commands()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await FluentActions.Awaiting(() => preparation.EnsureAsync(request, cancelled.Token)).Should().ThrowAsync<OperationCanceledException>();
        additions.Should().BeEmpty();
    }

    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
