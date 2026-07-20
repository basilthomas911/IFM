-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertOptionTrade] 
	-- Add the parameters for the stored procedure here
	@orderId int,
	@tradeId int,
	@tradeDate date,
	@maturityDate date,
	@tradeType varchar(64),
	@tradeState varchar(32),
	@tradeStrategy varchar(256)=null,
	@tradeAction varchar(64),
	@underlyingContractId varchar(64),
	@underlyingAssetType varchar(32),
	@isPrimaryTrade bit,
	@isHedgeTrade bit,
	@createdOn datetime=null,
	@createdBy varchar(64)=null,
	@updatedOn datetime=null,
	@updatedBy varchar(64)=null

AS
BEGIN
	if (exists(select * from dbo.option_trade where TradeId = @tradeId))
		update dbo.option_trade
		set TradeDate = @tradeDate,
			MaturityDate = @maturityDate,
			TradeType = @tradeType,
			TradeState = @tradeState,
			TradeStrategy = @tradeStrategy,
			TradeAction = @tradeAction,
			UnderlyingContractId = @underlyingContractId,
			UnderlyingAssetType = @underlyingAssetType,
			IsPrimaryTrade = @isPrimaryTrade,
			IsHedgeTrade = @isHedgeTrade,
			UpdatedOn = @updatedOn,
			UpdatedBy = @updatedBy
		where TradeId = @tradeId
	else
		insert into dbo.option_trade (
			OrderId,
			TradeId,
			TradeDate,
			MaturityDate,
			TradeType,
			TradeState,
			TradeStrategy,
			TradeAction,
			UnderlyingContractId,
			UnderlyingAssetType,
			IsPrimaryTrade,
			IsHedgeTrade,
			CreatedOn,
			CreatedBy,
			UpdatedOn,
			UpdatedBy
		) values (
			@orderId,
			@tradeId,
			@tradeDate,
			@maturityDate,
			@tradeType,
			@tradeState,
			@tradeStrategy,
			@tradeAction,
			@underlyingContractId,
			@underlyingAssetType,
			@isPrimaryTrade,
			@isHedgeTrade,
			@createdOn,
			@createdBy,
			@updatedOn,
			@updatedBy
		)

END
