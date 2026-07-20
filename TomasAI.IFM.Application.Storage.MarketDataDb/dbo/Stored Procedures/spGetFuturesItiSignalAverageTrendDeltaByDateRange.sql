-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesItiSignalAverageTrendDeltaByDateRange] 
	@symbol varchar(10),
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	declare @avgUpTrendDelta real
	declare @avgDownTrendDelta real

	select @avgUpTrendDelta = avg(TrendDelta) 
	FROM [marketdatadb].[dbo].[futures_iti_signal] fis
	join [marketdatadb].[dbo].[futures_contract] fc on fis.ContractId = fc.ContractId
	where IntrinsicTimeTrend = 'UpTrend'
	and IntrinsicTimeMode in ('TrendExtremeChanged','TrendReversalChanged')
	and fc.Symbol = @symbol
	and ValueDate between @startDate and @endDate
	--and PredictedDelta <> 0
	and FuturesRSI <> -1

	select @avgDownTrendDelta = avg(TrendDelta) 
	FROM [marketdatadb].[dbo].[futures_iti_signal] fis
	join [marketdatadb].[dbo].[futures_contract] fc on fis.ContractId = fc.ContractId
	where IntrinsicTimeTrend = 'DownTrend'
	and IntrinsicTimeMode in ('TrendExtremeChanged','TrendReversalChanged')
	and fc.Symbol = @symbol
	and ValueDate between @startDate and @endDate
	--and PredictedDelta <> 0
	and FuturesRSI <> -1

	select @symbol as Symbol,
		   @startDate as StartDate,
		   @endDate as EndDate,
		   IsNull(@avgUpTrendDelta,0) as UpTrendDelta,
		   IsNull(@avgDownTrendDelta,0)  as DownTrendDelta

END
