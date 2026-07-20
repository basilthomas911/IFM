-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-04-25
-- Description:	return commission for selected fund over date range
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundCommission] 
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
	select sum(Amount) as TradeCommission
	from fund_transaction
	where FundId = @fundId 
	and TransactionType = 'TradeCommission'
	and TransactionDate between @startDate and @endDate
END
