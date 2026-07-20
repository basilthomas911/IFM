-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertTradePlanAction] 
	-- Add the parameters for the stored procedure here
	@tradePlanId varchar(36),
	@orderId int,
	@tradeId int,
	@valueDate date,
	@actionType varchar(32),
	@actionSubType varchar(32),
	@actionState varchar(32),
	@actionDate datetime,
	@actionReason varchar(255) = null,
	@createdOn datetime,
	@createdBy varchar(64)
AS
BEGIN
	
    -- Insert statements for procedure here
	insert into dbo.trade_plan_action(
				[TradePlanId],
				[OrderId],
				[TradeId],
				[ValueDate],
				[ActionType],
				[ActionSubType],
				[ActionState],
				[ActionDate],
				[ActionReason],
				[CreatedOn],
				[CreatedBy]
			) values (
				@tradePlanId
			   ,@orderId
			   ,@tradeId
			   ,@valueDate
			   ,@actionType
			   ,@actionSubType
			   ,@actionState
			   ,@actionDate
			   ,@actionReason
			   ,@createdOn
			   ,@createdBy
  		)
END
