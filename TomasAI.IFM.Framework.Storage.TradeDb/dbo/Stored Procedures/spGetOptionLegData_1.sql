-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetOptionLegData] 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(32),
	@valueDate datetime,
	@daysToExpiry int,
	@tradeStatus varchar(32)
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	select [TradeId]
	  ,[TradeType]
      ,[ValueDate]
      ,[DaysToExpiry]
      ,[TradeStatus]
      ,[OptionLegId]
      ,[BidPrice]
      ,[AskPrice]
      ,[ImpliedVolatility]
      ,[Delta]
      ,[Gamma]
      ,[Theta]
      ,[Vega]
      ,[Rho]
      ,[CreatedOn]
      ,[CreatedBy]
      ,[UpdatedOn]
      ,[UpdatedBy]
	from [dbo].[option_leg_data]
	where TradeId = @tradeId
	and TradeType = @tradeType
	and ValueDate = @valueDate
	and DaysToExpiry = @daysToExpiry
	and TradeStatus = @tradeStatus

END
