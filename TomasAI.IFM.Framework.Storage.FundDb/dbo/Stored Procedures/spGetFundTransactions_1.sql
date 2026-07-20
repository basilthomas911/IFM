-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundTransactions] 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@startDate datetime,
	@endDate datetime
as
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	select [TransactionId]
      ,[TransactionDate]
      ,[TransactionType]
      ,[FundId]
      ,[OrderId]
      ,[TradeId]
      ,[TradeType]
      ,[ValueDate]
      ,[TradeStatus]
      ,[Description]
      ,[Amount]
      ,[Balance]
	from [dbo].[fund_transaction]
	where FundId = @fundId
	and TransactionDate >= @startDate 
	and TransactionDate <= @endDate
	
END
