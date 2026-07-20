-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertTradeFill 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@contractId varchar(32),
	@fillDate datetime,
	@price money,
	@quantity int,
	@commission money,
	@createdOn datetime,
	@createdBy varchar(64)

AS
BEGIN
	
	if not exists(select * from dbo.trade_fill where TradeId = @tradeId and ContractId = @contractId)
    -- Insert statements for procedure here
		insert into dbo.trade_fill(
			TradeId,
			ContractId,
			FillDate,
			Price,
			Quantity,
			Commission,
			CreatedOn,
			CreatedBy
		) values (
			@tradeId,
			@contractId,
			@fillDate,
			@price,
			@quantity,
			@commission,
			@createdOn,
			@createdBy )
	else
		update dbo.trade_fill
		set FillDate = @fillDate,
			Price = @price,
			Quantity = @quantity,
			Commission = @commission,
			CreatedOn = @createdOn,
			CreatedBy = @createdBy
		where TradeId = @tradeId
		and ContractId = @contractId

END
