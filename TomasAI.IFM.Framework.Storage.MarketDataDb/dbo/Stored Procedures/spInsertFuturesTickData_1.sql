-- =============================================
-- Author:		Basil Thomas
-- Create date: 2018-Jan-25
-- Description:	insert futures tick data
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesTickData] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@tickDate datetime,
	@tickTime bigint,
	@price real,
	@size int
AS
BEGIN
	if not exists(select * from dbo.futures_tick_data where ContractId = @contractId and TickTime = @tickTime) 
		insert into dbo.futures_tick_data(
			ContractId,
			TickDate,
			TickTime,
			Price,
			Size,
			ValueDate
		) values (
			@contractId,
			@tickDate,
			@tickTime,
			@price,
			@size,
			@valueDate
		)
	else
		update dbo.futures_tick_data
		set Price = @price,
			Size = @size,
			ValueDate = @valueDate
		where ContractId = @contractId 
		and TickTime = @tickTime
END
