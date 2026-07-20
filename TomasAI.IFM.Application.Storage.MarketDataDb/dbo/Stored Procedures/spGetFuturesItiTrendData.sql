-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesItiTrendData] 
	@symbol varchar(10),
	@startDate date,
	@endDate date
AS
BEGIN

	SELECT [Symbol]
      ,[ValueDate]
      ,[Timestamp]
      ,[SequenceId]
      ,[TrendDelta]
	  ,[TargetDelta]
      ,[TrendDirection]
      ,[TrendDirectionMode]
      ,[FuturesPrice]
	  ,[TrendExtreme]
      ,[FuturesMDI]
      ,[FuturesStdDev]
      ,[FuturesRSI]
      ,[FuturesFiftyDMA]
      ,[FuturesTwoHundredDMA]
	FROM [dbo].[futures_iti_trend_data]
	where Symbol = @symbol
	and ValueDate between @startDate and @endDate
	order by SequenceId

END
