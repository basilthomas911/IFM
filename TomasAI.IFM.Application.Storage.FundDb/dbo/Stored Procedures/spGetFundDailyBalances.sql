-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetFundDailyBalances
	-- Add the parameters for the stored procedure here
	@fundId int,
	@startDate date,
	@endDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select  ValueDate, max(Balance) as Balance
	from fund_transaction
	where ValueDate between @startDate and @endDate
	and FundId = @fundId
	group by ValueDate
	order by ValueDate desc

END
