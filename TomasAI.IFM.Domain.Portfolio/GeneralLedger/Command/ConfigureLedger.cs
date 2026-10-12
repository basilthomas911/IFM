using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Contracts;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command.Actor;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Persistence;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Command;

public static class ConfigureLedger
{
    /// <summary>Executes a canonical ledger configuration command.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="services">The enlisted persistence, receipt replay and projection services.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <returns>Command acceptance or its classified failure; durable completion remains tied to commit.</returns>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async ValueTask<ServiceResult<GuidResult>> ExecuteAsync(this ConfigureLedgerCommand command, LedgerConfigurationCommandServices services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(services);
        var replay = await (services.Database.ReadOperationAsync<LedgerConfigurationCompletedEvent>(command.PortfolioId, command.OperationId, command.InputSha256, cancellationToken)).ConfigureAwait(false);
        if (replay is not null) return await (replay.NotifyAsync(services.Projector, services.Logger)).ConfigureAwait(false);
        FinancialRequestValidation.Demand(command, "LedgerConfigure", DateTime.UtcNow);
        if (command.Body.Action == LedgerConfigurationAction.ReopenPeriod)
            FinancialRequestValidation.Demand(command, "LedgerPeriodReopen", DateTime.UtcNow);
        if (command.Body.Action is LedgerConfigurationAction.CreateBook or LedgerConfigurationAction.RefreshAuthority)
            await (command.ValidateAuthoritySourcesAsync(services.Sources, cancellationToken)).ConfigureAwait(false);
        if (command.Body.Action == LedgerConfigurationAction.QualifyDevelopmentBook)
            await (command.PrepareDevelopmentQualificationAsync(services, cancellationToken)).ConfigureAwait(false);
        var result = await (services.Store.ConfigureAsync(command, receipt => command.Complete(receipt), FinancialCanonicalHash.Compute, cancellationToken)).ConfigureAwait(false);
        return await (result.NotifyAsync(services.Projector, services.Logger)).ConfigureAwait(false);
    }

    /// <summary>Verifies that an empty development book is eligible for financial qualification.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="services">The enlisted persistence, receipt replay and projection services.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async Task PrepareDevelopmentQualificationAsync(this ConfigureLedgerCommand command, LedgerConfigurationCommandServices services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(services);
        FinancialRequestValidation.Demand(command, "LedgerImport", DateTime.UtcNow);
        if (services.DevelopmentPolicy?.IsDevelopmentEnvironment != true)
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied, "Development qualification services are unavailable.");
        var book = await (services.Database.ReadBookAsync(command.PortfolioId, cancellationToken)).ConfigureAwait(false);
        if (book is not { Environment: "Emulator", MigrationQualified: false } || book.Funds.Any(x => x.CanSpend) ||
            command.Body.Book is null || !string.Equals(FinancialCanonicalHash.Compute(book),FinancialCanonicalHash.Compute(command.Body.Book),StringComparison.Ordinal))
            throw new FinancialOperationException(FinancialReasons.AuthorityDenied, "Qualification requires the exact unqualified development book.");
        await (command.ValidateAuthoritySourcesAsync(services.Sources, cancellationToken)).ConfigureAwait(false);
    }

    /// <summary>Validates financial authority against current Portfolio event streams.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="sources">The sources business input.</param>
    /// <param name="cancellationToken">Cancels pre-commit work; an uncertain commit must be reconciled.</param>
    /// <exception cref="OperationCanceledException">Cancellation interrupts pre-commit work; committed or uncertain operations require reconciliation.</exception>
    public static async Task ValidateAuthoritySourcesAsync(this ConfigureLedgerCommand command, IPortfolioEventStore sources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(sources);
        var book = command.Body.Book ?? throw new FinancialOperationException(FinancialReasons.InvalidContract, "Book configuration is required.");
        var portfolio = await (sources.LoadPortfolioAsync(new(command.PortfolioId), cancellationToken)).ConfigureAwait(false);
        var funds = new Dictionary<int, TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioFundAggregate>();
        var policies = new Dictionary<int, TomasAI.IFM.Domain.Portfolio.Command.State.PortfolioFinancialPolicyAggregate>();
        foreach (var fund in book.Funds)
        {
            funds.Add(fund.FundId, await (sources.LoadFundAsync(new(command.PortfolioId, fund.FundId), cancellationToken)).ConfigureAwait(false));
            if (fund.CanSpend && !policies.ContainsKey(fund.Reference.PolicyId))
                policies.Add(fund.Reference.PolicyId, await (sources.LoadPolicyAsync(new(command.PortfolioId, fund.Reference.PolicyId), cancellationToken)).ConfigureAwait(false));
        }
        FinancialAuthorityModel.Validate(book, portfolio, funds, policies, DateTime.UtcNow);
    }

    /// <summary>Creates the completion event for a committed ledger configuration.</summary>
    /// <param name="command">The concrete business command and originating identity.</param>
    /// <param name="receipt">The verified committed financial receipt.</param>
    /// <returns>The operation result.</returns>
    public static LedgerConfigurationCompletedEvent Complete(this ConfigureLedgerCommand command, LedgerConfigurationReceipt receipt) 
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(receipt);
        return new()
    {
        Id = Guid.NewGuid(),
        Subject = new(ActorType.Event, ConfigureLedgerCommand.Actor, nameof(LedgerConfigurationCompletedEvent), command.EntityId.Format()),
        EntityId = command.EntityId,
        CommandId = command.CommandId,
        OperationId = command.OperationId,
        PortfolioId = command.PortfolioId,
        CorrelationId = command.CorrelationId,
        CausationId = command.CausationId,
        CommittedAtUtc = receipt.CommittedAtUtc,
        ReceivedOn = receipt.CommittedAtUtc,
        InputHash = command.InputSha256,
        AggregateId = command.EntityId.Format(),
        Receipt = receipt
    };
    }
}
