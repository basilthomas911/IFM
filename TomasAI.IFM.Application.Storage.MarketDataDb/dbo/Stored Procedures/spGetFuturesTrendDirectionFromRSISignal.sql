-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [spGetFuturesTrendDirectionFromRSISignal] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@valueDate date,
	@timestamp datetime,
	@lookbackInterval int,
	@startTime datetime,
	@endTime datetime
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	declare @upTrendCount int
	declare @downTrendCount int
	declare @trendDirection varchar(32)

	select @upTrendCount = count(*) from futures_rsi_signal
		where ContractId = @contractId and ValueDate = @valueDate
		and Timestamp between @startTime and @endTime and RSI >= 50

	select @downTrendCount = count(*) from futures_rsi_signal
		where ContractId = @contractId and ValueDate = @valueDate
		and Timestamp between @startTime and @endTime and RSI < 50

	select @trendDirection = 
		case
			when @upTrendCount > @downTrendCount then 'UpTrending'
			when @upTrendCount < @downTrendCount then 'DownTrending'
			when @upTrendCount = @downTrendCount then 'RangeBound'
		end

	select 
		@contractId as ContractId,
		@valueDate as ValueDate,
		@timestamp as Timestamp,
		@lookbackInterval as LookbackInterval,
		@upTrendCount as UpTrendCount,
		@downTrendCount as DownTrendCount,
		@trendDirection as TrendDirection

END
