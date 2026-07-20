-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesEodData]
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	select top 1
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
	where (ContractId = @contractId and ValueDate = @valueDate) or (ValueDate < @valueDate)
	order by ValueDate desc
	

END
