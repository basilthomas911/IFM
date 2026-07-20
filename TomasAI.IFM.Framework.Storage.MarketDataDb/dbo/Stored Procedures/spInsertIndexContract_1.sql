-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertIndexContract]
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@description varchar(256),
	@symbol varchar(64),
	@securityType varchar(10),
	@currency varchar(10),
	@exchange varchar(10)
AS
BEGIN
	if (exists(select * from dbo.index_contract ic where ic.ContractId = @contractId))
		update dbo.index_contract
		set Description = @description,
			Symbol = @symbol,
			SecurityType = @securityType,
			Currency = @currency,
			Exchange = @exchange
		where ContractId = @contractId
	else
		insert into dbo.index_contract(
			ContractId,
			Description,
			Symbol,
			SecurityType,
			Currency,
			Exchange
		) values (
			@contractId,
			@description,
			@symbol,
			@securityType,
			@currency,
			@exchange
		)
END
