-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesTdiSignal] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@timestamp datetime,
	@upTrendCount int,
	@downTrendCount int,
	@tdi varchar(32),
	@tdiStrength varchar(32)
AS
BEGIN

	delete from futures_tdi_signal
	where ContractId = @contractId
	and ValueDate = @valueDate
	and Timestamp = @timestamp

	insert into futures_tdi_signal(
		ContractId,
		ValueDate,
		Timestamp,
		UpTrendCount,
		DownTrendCount,
		TDI,
		TDIStrength
	) values(
		@contractId,
		@valueDate,
		@timestamp,
		@upTrendCount,
		@downTrendCount,
		@tdi,
		@tdiStrength
	)

END
