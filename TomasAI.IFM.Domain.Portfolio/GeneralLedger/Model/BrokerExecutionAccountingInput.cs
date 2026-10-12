using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Authoritative inputs for translating confirmed emulator fills into Fund postings.</summary>
#pragma warning disable CA1819 // Preserve the published snapshot array serialization contract.
public sealed record BrokerExecutionAccountingInput(
    FinancialPostingConfiguration Configuration,
    TradeOrderDefinition Order,
    ExecutionFillEvidence[] Fills,
    string MovementReference,
    DateTime ConfirmedAtUtc,
    IReadOnlyDictionary<Guid, decimal>? OpeningSignedSettlementByLeg = null);

#pragma warning restore CA1819
