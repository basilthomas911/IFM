-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesOptionContract] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@description varchar(264) = null,
	@symbol varchar(64),
	@localSymbol varchar(64),
	@securityType varchar(64),
	@currency varchar(10),
	@exchange varchar(64),
	@multiplier varchar(10),
	@contractMonth date,
	@strikePrice real,
	@optionType varchar(64)
AS
BEGIN
	
	if (exists(select * from dbo.futures_option_contract where ContractId = @contractId))
		update dbo.futures_option_contract
		set Description = @description,
			Symbol = @symbol,
			LocalSymbol = @localSymbol,
			SecurityType = @securityType,
			Currency = @currency,
			Exchange = @exchange,
			Multiplier = @multiplier,
			ContractMonth = @contractMonth,
			StrikePrice = @strikePrice,
			OptionType = @optionType
		where ContractId = @contractId
	else
		insert into dbo.futures_option_contract (
			ContractId,
			Description,
			Symbol,
			LocalSymbol,
			SecurityType,
			Currency,
			Exchange,
			Multiplier,
			ContractMonth,
			StrikePrice,
			OptionType
		) values (
			@contractId,
			@description,
			@symbol,
			@localSymbol,
			@securityType,
			@currency,
			@exchange,
			@multiplier,
			@contractMonth,
			@strikePrice,
			@optionType
		)

END
