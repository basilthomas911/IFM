-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spDeleteFundOrder]
	-- Add the parameters for the stored procedure here
	@orderId int,
	@removedOn datetime,
	@removedBy varchar(64)

AS
BEGIN

    -- Insert statements for procedure here
	update dbo.fund_order
	set Deleted = 1,
		UpdatedOn = @removedOn,
		UpdatedBy = @removedBy
	where OrderId = @orderId

	delete from dbo.fund_order_trade
	where OrderId = @orderId
END
