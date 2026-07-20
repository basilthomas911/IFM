-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetCurrentFuturesEodDataByDateRange]
	-- Add the parameters for the stored procedure here
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select 
		ContractId,
		ValueDate,
		OpenPrice,
		HighPrice,
		LowPrice,
		ClosePrice,
		Volume,
		DailyPercentChange,
		DailyStdDev,
		UpperBand,
		Mean,
		LowerBand,
		MarketTrend,
		MarketVolatility,
		MarketDirection,
		VixVolatility,
		FiftyDayMA,
		TwoHundredDayMA
	from dbo.futures_eod_data
	where ValueDate between @startDate and @endDate
	order by ValueDate desc

END
