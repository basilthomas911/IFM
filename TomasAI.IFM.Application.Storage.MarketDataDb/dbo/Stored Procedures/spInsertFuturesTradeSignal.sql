-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesTradeSignal] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@stdDev	real,
	@futuresPrice real,
	@priceChangePercent real,
	@fundRiskPercent real,
	@rsi real,
	@rsiSlope real,
	@trendType varchar(32),
	@trendStrength varchar(32),
	@tradeSignal varchar(32),
	@tdi varchar(32),
	@tdiStrength varchar(32),
	@mdi real,
	@mdiWatermark real,
	@upTrendingTrigger real,
	@downTrendingTrigger real,
	@entryTrigger real,
	@exitTrigger real,
	@trendDelta real,
	@trendExtreme real,
	@trendReversal real,
	@fiftyDma real,
	@twoHundredDma real,
	@tradeExecuteState varchar(32),
	@createdOn datetime,
	@createdBy varchar(64) = null
AS
BEGIN

	INSERT INTO [dbo].[futures_trade_signal]
			   ([ContractId]
			   ,[ValueDate]
			   ,[StdDev]
			   ,[FuturesPrice]
			   ,[PriceChangePercent]
			   ,[FundRiskPercent]
			   ,[RSI]
			   ,[RSISlope]
			   ,[TrendType]
			   ,[TrendStrength]
			   ,[TradeSignal]
			   ,[TDI]
			   ,[TDIStrength]
			   ,[MDI]
			   ,[MDIWatermark]
			   ,[UpTrendingTrigger]
			   ,[DownTrendingTrigger]
			   ,[EntryTrigger]
			   ,[ExitTrigger]
			   ,[TrendDelta]
			   ,[TrendExtreme]
			   ,[TrendReversal]
			   ,[FiftyDMA]
			   ,[TwoHundredDMA]
			   ,[TradeExecuteState]
			   ,[CreatedOn]
			   ,[CreatedBy])
	VALUES
		   (@contractId
		   ,@valueDate
		   ,@stdDev
		   ,@futuresPrice
		   ,@priceChangePercent
		   ,@fundRiskPercent
		   ,@rsi
		   ,@rsiSlope
		   ,@trendType
		   ,@trendStrength
		   ,@tradeSignal
		   ,@tdi
		   ,@tdiStrength
		   ,@mdi
		   ,@mdiWatermark
		   ,@upTrendingTrigger
		   ,@downTrendingTrigger
		   ,@entryTrigger
		   ,@exitTrigger
		   ,@trendDelta
		   ,@trendExtreme
		   ,@trendReversal
		   ,@fiftyDma
		   ,@twoHundredDma
		   ,@tradeExecuteState
		   ,@createdOn
		   ,@createdBy)

END
