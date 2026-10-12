using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.TradeSelection;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
using static TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection.TradeSelectionContracts;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.TradeSelection.Model;
/// <summary>Deterministic selection over frozen authority and accepted market evidence. Performs no I/O.</summary>
/// <summary>Pure deterministic selector calculation contract.</summary>
public interface ITradeSelectionCalculator
{
    TradeSelectionResult Calculate(ExecuteTradeSelectionPipelineCommand command);
}
