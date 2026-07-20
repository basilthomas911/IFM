-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertOptionLeg 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@contractId varchar(64),
	@quantity int,
	@strikePrice money,
	@optionLegType varchar(64),
	@optionLegAction varchar(64),
	@createdOn datetime = null,
	@createdBy varchar(64) = null,
	@updatedOn datetime = null,
	@updatedBy varchar(64) = null

AS
BEGIN

	if (exists(	select * from dbo.option_leg where TradeId = @tradeId and ContractId = @contractId))
		update dbo.option_leg
		set Quantity = @quantity,
			StrikePrice = @strikePrice,
			OptionLegType = @optionLegType,
			OptionLegAction = @optionLegAction,
			CreatedOn = @createdOn,
			CreatedBy = @createdBy,
			UpdatedOn = @updatedOn,
			UpdatedBy = @updatedBy
		where TradeId = @tradeId
		and ContractId = @contractId
	else
		insert into dbo.option_leg (
			TradeId,
			ContractId,
			Quantity,
			StrikePrice,
			OptionLegType,
			OptionLegAction,
			CreatedOn,
			CreatedBy,
			UpdatedOn,
			UpdatedBy
		) values (
			@tradeId,
			@contractId,
			@quantity,
			@strikePrice,
			@optionLegType,
			@optionLegAction,
			@createdOn,
			@createdBy,
			@updatedOn,
			@updatedBy
		)
END
