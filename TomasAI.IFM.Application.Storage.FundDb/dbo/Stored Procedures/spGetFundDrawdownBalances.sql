-- =============================================
-- Author:		
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundDrawdownBalances]
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
	SELECT 
	  @fundId as FundId,
	  (select Balance from fund_transaction where TransactionId in (select min(TransactionId) from fund_transaction where FundId = @fundId and TransactionDate >= @startDate)) as StartBalance,
	  (select Balance from fund_transaction where TransactionId in (select max(TransactionId) from fund_transaction where FundId = @fundId and TransactionDate <= @endDate)) as EndBalance
END
