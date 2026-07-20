-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesTrendDirection]
	-- Add the parameters for the stored procedure here
	@contractId varchar(32),
	@valueDate date,
	@timestamp datetime,
	@lookbackInterval int,
	@upTrendCount int,
	@downTrendCount int,
	@trendDirection varchar(32)
AS
BEGIN

	delete from futures_trend_direction
	where ContractId = @contractId
	and ValueDate = @valueDate
	and Timestamp = @timeStamp

	insert into futures_trend_direction(
		ContractId,
		ValueDate,
		Timestamp,
		LookbackInterval,
		UpTrendCount,
		DownTrendCount,
		TrendDirection
	) values (
		@contractId,
		@valueDate,
		@timestamp,
		@lookbackInterval,
		@upTrendCount,
		@downTrendCount,
		@trendDirection
	)

END
