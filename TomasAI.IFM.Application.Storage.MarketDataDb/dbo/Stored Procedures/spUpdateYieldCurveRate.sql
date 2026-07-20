-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateYieldCurveRate]

	@valueDate date,
	@oneMonth real,
	@twoMonth real,
	@threeMonth real,
	@sixMonth real,
	@oneYear real,
	@twoYear real,
	@threeYear real,
	@fiveYear real,
	@sevenYear real,
	@tenYear real,
	@twentyYear real,
	@thirtyYear real

AS
BEGIN
	
	if exists(select * from dbo.yield_curve_rates where Valuedate = @valueDate)
		update [dbo].[yield_curve_rates]
		set ValueDate = @valueDate,
			OneMonth = @oneMonth,
			TwoMonth = @twoMonth,
			ThreeMonth = @threeMonth,
			SixMonth = @sixMonth,
			OneYear = @oneYear,
			TwoYear = @twoYear,
			ThreeYear = @threeYear,
			FiveYear = @fiveYear,
			SevenYear = @sevenYear,
			TenYear = @tenYear,
			TwentyYear = @twentyYear, 
			ThirtyYear = @thirtyYear
		 where ValueDate = @valueDate
	

END
