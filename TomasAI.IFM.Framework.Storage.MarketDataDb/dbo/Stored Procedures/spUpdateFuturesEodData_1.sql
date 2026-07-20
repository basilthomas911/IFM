-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spUpdateFuturesEodData] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@openPrice real,
	@highPrice real,
	@lowPrice real,
	@closePrice real,
	@volume		int
AS
BEGIN
	update dbo.futures_eod_data
	set OpenPrice = @openPrice,
		HighPrice = @highPrice,
		LowPrice = @lowPrice,
		ClosePrice = @closePrice,
		Volume = @volume
	where ContractId = @contractId
	and ValueDate = @valueDate
END
