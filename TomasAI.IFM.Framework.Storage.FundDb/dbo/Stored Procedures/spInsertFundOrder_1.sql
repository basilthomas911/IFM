
-- =============================================
-- Author:		basil thomas
-- Create date: 2018-06-9
-- Description:	create or update fund order
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFundOrder] 
	-- Add the parameters for the stored procedure here
	@fundId int,
	@orderId int,
	@orderDate datetime,
	@orderStatus varchar(32),
	@reference varchar(256) = null,
	@createdOn datetime,
	@createdBy varchar(64) = null
AS
BEGIN

	if not exists(select * from dbo.fund_order where OrderId = @orderId)
		insert into dbo.fund_order(
			FundId,
			OrderId,
			OrderDate,
			OrderStatus,
			Reference,
			Deleted,
			CreatedOn,
			CreatedBy,
			UpdatedOn,
			UpdatedBy
		) values (
			@fundId,
			@orderId,
			@orderDate,
			@orderStatus,
			@reference,
			0,
			@createdOn,
			@createdBy,
			@createdOn,
			@createdBy
		)

END
