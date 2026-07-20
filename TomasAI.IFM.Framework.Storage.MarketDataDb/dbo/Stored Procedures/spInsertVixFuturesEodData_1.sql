-- =============================================
-- Author:		Basil Thomas
-- Create date: 2020-11-18
-- Description:	insert vix futures tick data
-- =============================================
CREATE PROCEDURE [dbo].[spInsertVixFuturesEodData] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@price real,
	@size int
AS
BEGIN
	if (exists(select * from dbo.vix_futures_eod_data where ContractId = @contractId and ValueDate = @valueDate))
		update dbo.vix_futures_eod_data
		set 
			OpenPrice = (select td.Price from futures_tick_data td where td.ContractId = @contractId and Valuedate = @valueDate and td.TickTime = (
				select min(ftd.TickTime) from futures_tick_data ftd where ftd.ContractId = td.ContractId and ftd.ValueDate = td.ValueDate
			)),
			HighPrice = (select max(td.Price) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			LowPrice = (select min(td.Price) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate),
			ClosePrice = @price,
			Volume = (select sum(td.Size) from futures_tick_data td where td.ContractId = @contractId and td.ValueDate = @valueDate) + @size
			
		where ContractId = @contractId and ValueDate = @valueDate
	else
		insert into dbo.vix_futures_eod_data (
			ContractId,
			ValueDate,
			OpenPrice,
			HighPrice,
			LowPrice,
			ClosePrice,
			Volume
		) values (
			@contractId,
			@valueDate,
			@price,
			@price,
			@price,
			@price,
			@size
		)
END
