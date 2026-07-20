-- =============================================
-- Author:		Basil Thomas
-- Create date: 2019-04-25
-- Description:	return pnl for selected fund over date range
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundPnl] 
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
	select FundId, TransactionDate, OrderId, TradeId, TradeType, sum(Amount) as Pnl
	from fund_transaction
	where FundId = @fundId 
	and TransactionType = 'RealizedTradePnl'
	and TransactionDate between @startDate and @endDate
	group by FundId, TransactionDate, Orderid, TradeId, TradeType
END
