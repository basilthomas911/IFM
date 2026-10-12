using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesRsiSignal.Command.Model;

#pragma warning disable CA1819 // Preserve the published snapshot array serialization contract.
public sealed record FuturesRsiSeedDecision(FuturesTradeSessionBarReadModel[] Observations, string Reason);

#pragma warning restore CA1819
