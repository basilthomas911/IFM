using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>Typed Portfolio boundary for confirmed broker executions.</summary>
public interface IPortfolioTradeAccountingApi
{
    /// <summary>Posts confirmed settlement, commission, and closing realized P&amp;L idempotently.</summary>
    ValueTask<ServiceResult<Guid>> PostConfirmedExecutionAsync(OrderExecutionDefinition execution,
        Guid sourceEventId, DateTime confirmedAtUtc, CancellationToken cancellationToken = default);
}

/// <summary>Builds the authoritative Fund batch and sends it through the GeneralLedger command actor.</summary>
public sealed class BrokerExecutionAccountingApi(IFinancialQueryStore financialQueries,
    IDbContextFactory databases, IActorService actors) : IPortfolioTradeAccountingApi
{
    /// <inheritdoc />
    public async ValueTask<ServiceResult<Guid>> PostConfirmedExecutionAsync(
        OrderExecutionDefinition execution,
        Guid sourceEventId,
        DateTime confirmedAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        if (execution.Status != OrderExecutionStatus.Filled || execution.Fills.Length == 0 ||
            !execution.Id.IsValid || sourceEventId == Guid.Empty || confirmedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A filled execution, source event and UTC confirmation time are required.");

        var access = new FinancialAccess("BrokerExecutionAccounting", ["PortfolioAdministrator"]);
        var scope = new FinancialReadScope
        {
            PortfolioId = execution.TradeOrderId.PortfolioId,
            FundId = execution.TradeOrderId.FundId,
            Access = access
        };
        var accountingDate = DateOnly.FromDateTime(execution.Fills.Min(static fill => fill.FilledAtUtc));
        var selected = await financialQueries.ReadAsync(scope,
            new GetFinancialPostingConfigurationRequest(accountingDate), cancellationToken).ConfigureAwait(false);
        if (selected.Status != FinancialReadStatus.Found || selected.Value is null || !selected.Value.PeriodOpen)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.CONFIGURATION_UNAVAILABLE");

        var openingBasis = execution.PositionType == TradeOrderPositionType.Closing
            ? await LoadOpeningBasisAsync(execution, cancellationToken).ConfigureAwait(false)
            : null;
        var movementReference = $"BrokerExecution:{execution.Id.Format()}";
        var batch = BrokerExecutionAccountingModel.Create(new(
            selected.Value, execution.Order, execution.Fills, movementReference,
            confirmedAtUtc, openingBasis));
        var operationId = DeterministicId("portfolio-accounting", execution.Id.Format());
        var command = new PostFundTransactionsCommand
        {
            CommandId = DeterministicId("post-portfolio-accounting", execution.Id.Format()),
            Subject = new(ActorType.Command, PostFundTransactionsCommand.Actor,
                PostFundTransactionsCommand.Verb, new LedgerPortfolioId(execution.TradeOrderId.PortfolioId).Format()),
            EntityId = new(execution.TradeOrderId.PortfolioId),
            OperationId = operationId,
            PortfolioId = execution.TradeOrderId.PortfolioId,
            CorrelationId = execution.ExecutionAttemptId,
            CausationId = sourceEventId,
            RequestedAtUtc = confirmedAtUtc,
            ExpiresAtUtc = confirmedAtUtc.AddDays(1),
            ExpectedFinancialRevision = selected.FinancialRevision,
            Body = batch,
            Access = access
        };
        command = command with { InputSha256 = FinancialCanonicalHash.Request(command) };
        return await actors.SendAsync<PostFundTransactionsCommand, LedgerPortfolioId>(
            command, command.EntityId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<Guid, decimal>> LoadOpeningBasisAsync(
        OrderExecutionDefinition execution,
        CancellationToken cancellationToken)
    {
        if (execution.TargetPositionId is not { IsValid: true } positionId)
            throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.CLOSE_POSITION_REQUIRED");
        var trade = await databases.TradeDb.GetEstablishedTradeAsync(positionId.Trade, cancellationToken)
            .ConfigureAwait(false) ?? throw new InvalidOperationException("PORTFOLIO_ACCOUNTING.OPENING_TRADE_NOT_FOUND");
        var openingLegs = trade.Legs.ToDictionary(static leg => leg.TradeLegId);
        var basis = new Dictionary<Guid, decimal>();
        foreach (var closingLeg in execution.Components.SelectMany(static component => component.Legs))
        {
            var matching = trade.OriginalFills
                .Where(fill => string.Equals(fill.ContractId, closingLeg.ContractId, StringComparison.Ordinal))
                .ToArray();
            if (matching.Length == 0)
                throw new InvalidOperationException($"PORTFOLIO_ACCOUNTING.OPENING_BASIS_NOT_FOUND;{closingLeg.ContractId}");
            decimal signed = 0;
            foreach (var fill in matching)
            {
                if (!openingLegs.TryGetValue(fill.TradeLegId, out var openingLeg) || openingLeg.CashMultiplier <= 0)
                    throw new InvalidOperationException($"PORTFOLIO_ACCOUNTING.OPENING_LEG_INVALID;{fill.TradeLegId}");
                signed += fill.Price * fill.SignedQuantity * openingLeg.CashMultiplier;
            }
            basis.Add(closingLeg.TradeLegId, decimal.Round(signed, 2, MidpointRounding.ToEven));
        }
        return basis;
    }

    private static Guid DeterministicId(string purpose, string identity) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"{purpose}|{identity}"))[..16]);
}
