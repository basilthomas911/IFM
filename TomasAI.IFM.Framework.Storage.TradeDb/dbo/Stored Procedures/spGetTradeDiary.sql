-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spGetTradeDiary 
	-- Add the parameters for the stored procedure here
	@orderid int,
	@tradeId int
AS
BEGIN


	select [EntryId]
      ,[EntryDate]
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
      ,[ActionData]
	from [dbo].[trade_diary]
	where Orderid = @orderid
	and TradeId = @tradeId
	order by EntryDate desc


END
