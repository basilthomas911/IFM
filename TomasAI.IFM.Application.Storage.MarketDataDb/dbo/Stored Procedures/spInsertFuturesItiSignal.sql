-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesItiSignal] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date,
	@intrinsicTime datetime,
	@intrinsicTimeLength real,
	@intrinsicPrice real,
	@intrinsicTimeTrend varchar(32),
	@intrinsicTimeMode varchar(32),
	@trendPrice real,
	@trendExtreme real,
	@trendReversal real,
	@lambda real,
	@targetDelta real,
	@predictedDelta real,
	@trendDelta real,
	@upTrendTrigger real,
	@downTrendTrigger real,
	@futuresPercentChange real,
	@futuresStdDev real,
	@futuresMDI real,
	@futuresRSI real,
	@futuresRSISlope real,
	@futuresFiftyDMA real,
	@futuresTwoHundredDMA real,
	@tradeState varchar(32),
	@upTrendCoastLineCounter int,
	@downTrendCoastLineCounter int
AS
BEGIN

	INSERT INTO [dbo].[futures_iti_signal]
           ([ContractId]
           ,[ValueDate]
           ,[IntrinsicTime]
           ,[IntrinsicTimeLength]
           ,[IntrinsicPrice]
           ,[IntrinsicTimeTrend]
           ,[IntrinsicTimeMode]
           ,[TrendPrice]
           ,[TrendExtreme]
           ,[TrendReversal]
           ,[Lambda]
		   ,[TargetDelta]
		   ,[PredictedDelta]
		   ,[TrendDelta]
           ,[UpTrendTrigger]
           ,[DownTrendTrigger]
		   ,[FuturesPercentChange]
		   ,[FuturesStdDev]
		   ,[FuturesMDI]
		   ,[FuturesRSI]
		   ,[FuturesRSISlope]
		   ,[FuturesFiftyDMA]
		   ,[FuturesTwoHundredDMA]
		   ,[TradeState]
	       ,[UpTrendCoastLineCounter]
	       ,[DownTrendCoastLineCounter])
     VALUES
           (@contractId
           ,@valueDate
           ,@intrinsicTime
           ,@intrinsicTimeLength
           ,@intrinsicPrice
           ,@intrinsicTimeTrend
           ,@intrinsicTimeMode
           ,@trendPrice
           ,@trendExtreme
           ,@trendReversal
           ,@lambda
		   ,@targetDelta
		   ,@predictedDelta
		   ,@trendDelta
           ,@upTrendTrigger
           ,@downTrendTrigger
		   ,@futuresPercentChange
		   ,@futuresStdDev
		   ,@futuresMDI
		   ,@futuresRSI
		   ,@futuresRSISlope
		   ,@futuresFiftyDMA
		   ,@futuresTwoHundredDMA
		   ,@tradeState
		   ,@upTrendCoastLineCounter
		   ,@downTrendCoastLineCounter)

END
