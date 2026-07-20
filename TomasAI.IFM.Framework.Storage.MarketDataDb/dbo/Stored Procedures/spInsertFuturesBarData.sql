-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE spInsertFuturesBarData 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@symbol varchar(64),
	@valueDate datetime,
	@openPrice real,
	@highPrice real,
	@lowPrice real,
	@closePrice real,
	@volume int

AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	insert into dbo.futures_bar_data (
		ContractId,
		Symbol,
		ValueDate,
		OpenPrice,
		HighPrice,
		LowPrice,
		ClosePrice,
		Volume
	) values (
		@contractId,
		@symbol,
		@valueDate,
		@openPrice,
		@highPrice,
		@lowPrice,
		@closePrice,
		@volume 
	)

END
