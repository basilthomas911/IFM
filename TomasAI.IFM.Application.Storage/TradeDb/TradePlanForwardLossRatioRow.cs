using TomasAI.IFM.Framework.Storage;

namespace TomasAI.IFM.Application.Storage.TradeDb;

internal sealed record TradePlanForwardLossRatioRow(double ForwardLossRatio, long SequenceId);
