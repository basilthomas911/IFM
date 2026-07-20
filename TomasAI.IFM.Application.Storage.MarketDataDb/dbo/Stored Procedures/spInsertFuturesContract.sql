-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesContract]
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@description varchar(256),
	@symbol varchar(64),
	@localSymbol varchar(10),
	@securityType varchar(10),
	@currency varchar(10),
	@exchange varchar(10),
	@multiplier varchar(10),
	@lastTradeDate date,
	@currentlyTraded bit
AS
BEGIN
	if (exists(select * from dbo.futures_contract fc where fc.ContractId = @contractId))
		update dbo.futures_contract
		set Description = @description,
			Symbol = @symbol,
			LocalSymbol = @localSymbol,
			SecurityType = @securityType,
			Currency = @currency,
			Exchange = @exchange,
			Multiplier = @multiplier,
			LastTradeDate = @lastTradeDate,
			CurrentlyTraded = @currentlyTraded
		where ContractId = @contractId
	else
		insert into dbo.futures_contract(
			ContractId,
			Description,
			Symbol,
			LocalSymbol,
			SecurityType,
			Currency,
			Exchange,
			Multiplier,
			LastTradeDate,
			CurrentlyTraded
		) values (
			@contractId,
			@description,
			@symbol,
			@localSymbol,
			@securityType,
			@currency,
			@exchange,
			@multiplier,
			@lastTradeDate,
			@currentlyTraded
		)
END
