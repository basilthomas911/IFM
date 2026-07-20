-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesItiSignalTrendData] 
	-- Add the parameters for the stored procedure here
	@symbol varchar(32), 
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

 
	SELECT fis.ContractId
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
      --,TrendExtreme-IntrinsicPrice as TrendDelta
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
	FROM [dbo].[futures_iti_signal] fis 
	join futures_contract fc on fis.ContractId = fc.ContractId
	where fc.Symbol = @symbol
	and fis.ValueDate between @startDate and @endDate
	and fis.IntrinsicTimeMode in ('TrendExtremeChanged','TrendReversalChanged','TrendDirectionChanged') 
	and fis.FuturesRSI <> -1
	order by fis.SequenceId

END

