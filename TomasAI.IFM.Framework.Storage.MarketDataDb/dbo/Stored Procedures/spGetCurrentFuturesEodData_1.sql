-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetCurrentFuturesEodData]
	-- Add the parameters for the stored procedure here
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
		Size,
		DailyPercentChange,
		DailyStdDev,
		UpperBand,
		Mean,
		LowerBand,
		MarketTrend,
		MarketVolatility,
		MarketVolatilityTrend,
		PutSpreadProbability,
		PutSpreadStdDev,
		CallSpreadProbability,
		CallSpreadStdDev,
		RateOfReturn,
		NearestPutStrike,
		NearestCallStrike
	from dbo.futures_eod_data
	where ValueDate <= @valueDate
	order by ValueDate desc
	

END
