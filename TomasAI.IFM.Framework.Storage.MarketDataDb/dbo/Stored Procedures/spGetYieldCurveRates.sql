-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetYieldCurveRates] 
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select 
		ValueDate,
		OneMonth,
		TwoMonth,
		ThreeMonth,
		SixMonth,
		OneYear,
		TwoYear,
		ThreeYear,
		FiveYear,
		SevenYear,
		TenYear,
		TwentyYear,
		ThirtyYear
	from dbo.yield_curve_rates
	where ValueDate between @startDate and @endDate
	order by ValueDate desc

END
