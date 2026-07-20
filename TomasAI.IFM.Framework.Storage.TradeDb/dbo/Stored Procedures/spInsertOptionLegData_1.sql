-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertOptionLegData]
	-- Add the parameters for the stored procedure here
	@tradeId int,
	@tradeType varchar(32),
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
	@createdOn datetime = null,
	@createdBy varchar(64) = null,
	@updatedOn datetime = null,
	@updatedBy varchar(64) = null
AS
BEGIN

	if (exists(select * from dbo.option_leg_data where TradeId = @tradeId and TradeType = @tradeType and ValueDate = @valueDate
				and DaysToExpiry = @daysToExpiry and TradeStatus = @tradeStatus and OptionLegId = @optionLegId))
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
		where TradeId = @tradeId 
		and TradeType = @tradeType
		and ValueDate = @valueDate
		and DaysToExpiry = @daysToExpiry 
		and TradeStatus = @tradeStatus 
		and OptionLegId = @optionLegId
	else
		insert into dbo.option_leg_data (
			TradeId,
			TradeType,
			ValueDate,
			DaysToExpiry,
			TradeStatus,
			OptionLegId,
			BidPrice,
			AskPrice,
			ImpliedVolatility,
			Delta,
			Gamma,
			Theta,
			Vega,
			Rho,
			CreatedOn,
			CreatedBy,
			UpdatedOn,
			UpdatedBy
		) values (
			@tradeId,
			@tradeType,
			@valueDate,
			@daysToExpiry,
			@tradeStatus,
			@optionLegId,
			@bidPrice,
			@askPrice,
			@impliedVolatility,
			@delta,
			@gamma,
			@theta,
			@vega,
			@rho,
			@createdOn,
			@createdBy,
			@updatedOn,
			@updatedBy
		)

END
