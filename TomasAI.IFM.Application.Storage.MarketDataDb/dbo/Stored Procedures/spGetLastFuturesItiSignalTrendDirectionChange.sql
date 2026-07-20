-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetLastFuturesItiSignalTrendDirectionChange] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(32), 
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

 
	SELECT [ContractId]
      ,[ValueDate]
      ,[IntrinsicTime]
      ,[SequenceId]
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
	  ,[DownTrendCoastLineCounter]
	FROM [dbo].[futures_iti_signal]
	where SequenceId = (select Max(SequenceId) from futures_iti_signal
	                  where ContractId = @contractId 
					  and ValueDate = @valueDate
					  and IntrinsicTimeMode = 'TrendDirectionChanged')

END