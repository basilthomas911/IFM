-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetBenchmarkReturns 
	-- Add the parameters for the stored procedure here
	@symbol varchar(10),
	@startDate datetime,
	@endDate datetime
AS
BEGIN

    -- Insert statements for procedure here
	select ValueDate, DailyPercentChange as BenchmarkReturn
	from futures_eod_data
	where ValueDate between @startDate and @endDate

END
