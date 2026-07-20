-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spUpdateFundBalance 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@balance money
AS
BEGIN

	update fund
	set Balance = @balance
	where FundId = @fundId

END
