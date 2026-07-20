-- =============================================
-- Author:		Basil Thomas
-- Create date: Mar 27, 2020
-- Description:	return fund id from order id
-- =============================================
CREATE PROCEDURE spGetFundIdFromOrderId 
	-- Add the parameters for the stored procedure here
	@orderId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	select FundId 
	from Fund_Order
	where OrderId = @orderId

END
