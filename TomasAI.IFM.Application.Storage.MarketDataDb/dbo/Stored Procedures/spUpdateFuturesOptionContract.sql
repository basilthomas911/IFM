-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFuturesOptionContract] 
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

END
