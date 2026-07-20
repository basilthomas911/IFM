-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-05-18
-- Description:	return all order with loss amount
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundLossOrders] 
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
	select OrderId, sum(Amount) as Amount
	from fund_transaction
	where FundId = @fundId 
	and TransactionType <> 'OpeningTrade'
	and ValueDate between @startDate and @endDate
	group by OrderId having sum(Amount) < 0

END
