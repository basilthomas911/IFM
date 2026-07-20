-- =============================================
-- Author:		Basil Thomas
-- Create date: 2023-02-27
-- Description:	insert futures rsi signal
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesRsiSignal] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64), 
	@valueDate date,
	@timestamp datetime,
	@signalType varchar(32),
	@price money,
	@priceChange money,
	@priceGain money,
	@priceLoss money,
	@averagePriceGain money,
	@averagePriceLoss money,
	@rs real,
	@rsi real,
	@rsiAverage real,
	@rsiSlope real,
	@windowSize real
AS
BEGIN
	
	delete from futures_rsi_signal
	where ContractId = @contractId
	and ValueDate = @valueDate
	and SignalType = @signalType
	and Timestamp = @timestamp

	insert into futures_rsi_signal(
		ContractId,
		ValueDate,
		Timestamp,
		SignalType,
		Price,
		PriceChange,
		PriceGain,
		PriceLoss,
		AveragePriceGain,
		AveragePriceLoss,
		RS,
		RSI,
		RSIAverage,
		RSISlope,
		WindowSize
	) values (
		@contractId,
		@valueDate,
		@timestamp,
		@signalType,
		@price,
		@priceChange,
		@priceGain,
		@priceLoss,
		@averagePriceGain,
		@averagePriceLoss,
		@rs,
		@rsi,
		@rsiAverage,
		@rsiSlope,
		@windowSize
	)

END
