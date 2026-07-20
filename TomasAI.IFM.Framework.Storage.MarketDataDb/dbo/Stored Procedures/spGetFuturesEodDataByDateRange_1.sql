-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesEodDataByDateRange]
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
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
		FiveDayXMA
	from dbo.futures_eod_data
	where ContractId = @contractId 
	and ValueDate between @startDate and @endDate
	order by ValueDate desc

END
