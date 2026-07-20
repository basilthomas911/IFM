-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertTradeDiary
	-- Add the parameters for the stored procedure here
	@entryDate datetime,
	@orderId int,
	@tradeId int,
	@valueDate date,
	@tradeStatus varchar(32),
	@actionSource varchar(32),
	@actionType varchar(32),
	@actionSubType varchar(32),
	@actionState varchar(32),
	@actionReason varchar(255),
	@actionDataType varchar(255) = null,
	@actionData ntext = null

AS
BEGIN

	INSERT INTO [dbo].[trade_diary]
           ([EntryDate]
           ,[OrderId]
           ,[TradeId]
           ,[ValueDate]
           ,[TradeStatus]
           ,[ActionSource]
           ,[ActionType]
           ,[ActionSubType]
           ,[ActionState]
           ,[ActionReason]
           ,[ActionDataType]
           ,[ActionData])
     VALUES
           (@entryDate
           ,@orderid
           ,@tradeId
           ,@valueDate
           ,@tradeStatus
           ,@actionSource
           ,@actionType
           ,@actionSubType
           ,@actionState
           ,@actionReason
           ,@actionDataType
           ,@actionData)

END
