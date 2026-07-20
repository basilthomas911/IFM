-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spUpdateOptionLegData 
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@valueDate datetime,
	@daysToExpiry int,
	@tradeStatus varchar(64),
	@optionLegId varchar(64),
	@bidPrice money,
	@askPrice money,
	@impliedVolatility real,
	@delta real,
	@gamma real,
	@theta real,
	@vega real,
	@rho real,
	@updatedOn datetime,
	@updatedBy varchar(64)
AS
BEGIN

	update dbo.option_leg_data
	set BidPrice = @bidPrice,
		AskPrice = @askPrice,
		ImpliedVolatility = @impliedVolatility,
		Delta = @delta,
		Gamma = @gamma,
		Theta = @theta,
		Vega = @vega,
		Rho = @rho,
		UpdatedOn = @updatedOn,
		UpdatedBy = @updatedBy
	where TradeId = @tradeid
	and ValueDate = @valueDate
	and DaysToExpiry = @daysToExpiry
	and TradeStatus = @tradeStatus
	and OptionlegId = @optionLegId

END
