-- =============================================
-- Author:		Basil Thomas
-- Create date: July 14, 2018
-- Description:	update futures contract
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFuturesContract]
    @originalContractId varchar(64),
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

	-- delete original futures contract
	delete from dbo.futures_contract 
	where ContractId = @originalContractId

	-- insert changed contract
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
