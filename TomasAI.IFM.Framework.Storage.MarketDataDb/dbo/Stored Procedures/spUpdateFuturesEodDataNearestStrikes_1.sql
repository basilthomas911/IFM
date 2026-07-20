-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFuturesEodDataNearestStrikes] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@nearestPutStrike real,
	@nearestCallStrike real
AS
BEGIN
	update dbo.futures_eod_data
	set NearestPutStrike = @nearestPutStrike,
		NearestCallStrike = @nearestCallStrike
	where ContractId = @contractId
	and ValueDate = @valueDate
END
