-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFundOrderStatus] 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@orderId int,
	@orderStatus varchar(32)
AS
BEGIN
	
	update dbo.fund_order
	set OrderStatus = @orderStatus
	where FundId = @fundId
	and OrderId = @orderId

END
