-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFundStartingBalance]
	-- Add the parameters for the stored procedure here
	@fundId int,
	@startDate date

AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select Balance 
	from dbo.fund_transaction
	where TransactionId = (select min(TransactionId) 
	                       from dbo.fund_transaction 
						   where FundId = @fundId 
						   and ValueDate >= @startDate )

END
