-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesItiSignalAveragePredictedTrendDeltaByDateRange] 
	@symbol varchar(10),
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	declare @avgUpTrendPredictedDelta real
	declare @avgDownTrendPredictedDelta real

	select @avgUpTrendPredictedDelta = avg(PredictedDelta)
	FROM [marketdatadb].[dbo].[futures_iti_signal] fis
	join [marketdatadb].[dbo].[futures_contract] fc on fis.ContractId = fc.ContractId
	where IntrinsicTimeTrend = 'UpTrend'
	and IntrinsicTimeMode in ('TrendExtremeChanged','TrendReversalChanged','TrendDirectionChanged')
	and fc.Symbol = @symbol
	and ValueDate between @startDate and @endDate
	and FuturesRSI <> -1

	select @avgDownTrendPredictedDelta = avg(PredictedDelta) 
	FROM [marketdatadb].[dbo].[futures_iti_signal] fis
	join [marketdatadb].[dbo].[futures_contract] fc on fis.ContractId = fc.ContractId
	where IntrinsicTimeTrend = 'DownTrend'
	and IntrinsicTimeMode in ('TrendExtremeChanged','TrendReversalChanged','TrendDirectionChanged')
	and fc.Symbol = @symbol
	and ValueDate between @startDate and @endDate
	and FuturesRSI <> -1

	select @symbol as Symbol,
		   @startDate as StartDate,
		   @endDate as EndDate,
		   IsNull(@avgUpTrendPredictedDelta,0) as PredictedUpTrendDelta,
		   IsNull(@avgDownTrendPredictedDelta,0)  as PredictedDownTrendDelta

END
