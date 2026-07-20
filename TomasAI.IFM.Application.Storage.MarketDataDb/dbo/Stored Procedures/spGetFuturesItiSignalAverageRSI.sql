-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetFuturesItiSignalAverageRSI] 
	@contractId varchar(32),
	@valueDate date
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;
	declare @maxUpTrendValueDate date
	declare @maxDownTrendValueDate date
	declare @avgUpTrendFuturesRsi real
	declare @avgDownTrendFuturesRsi real

	select @maxUpTrendValueDate = max(ValueDate) from futures_iti_signal 
	where ContractId = @contractId 
	and ValueDate <= @valueDate 
	and IntrinsicTimeTrend = 'UpTrend'

	select @maxDownTrendValueDate = max(ValueDate) from futures_iti_signal 
	where ContractId = @contractId 
	and ValueDate <= @valueDate 
	and IntrinsicTimeTrend = 'DownTrend'

	select @avgUpTrendFuturesRsi = avg(FuturesRSI)
	FROM [marketdatadb].[dbo].[futures_iti_signal]
	where IntrinsicTimeTrend = 'UpTrend'
	and IntrinsicTimeMode in ('TrendExtremeChanged','TrendReversalChanged','TrendDirectionChanged')
	and ContractId = @contractId
	and ValueDate = @maxUpTrendValueDate
	and FuturesRSI <> -1
	and SequenceId > (select max(SequenceId)
					from futures_iti_signal
					where IntrinsicTimeTrend = 'UpTrend'
					and IntrinsicTimeMode = 'TrendDirectionChanged'
					and ContractId = @contractId
					and ValueDate = @maxUpTrendValueDate)

	select @avgDownTrendFuturesRsi = avg(FuturesRSI)
	FROM [marketdatadb].[dbo].[futures_iti_signal]
	where IntrinsicTimeTrend = 'DownTrend'
	and IntrinsicTimeMode in ('TrendExtremeChanged','TrendReversalChanged','TrendDirectionChanged')
	and ContractId = @contractId
	and ValueDate = @maxDownTrendValueDate
	and FuturesRSI <> -1
	and SequenceId > (select max(SequenceId)
					from futures_iti_signal
					where IntrinsicTimeTrend = 'DownTrend'
					and IntrinsicTimeMode = 'TrendDirectionChanged'
					and ContractId = @contractId
					and ValueDate = @maxDownTrendValueDate)

	select @contractId as ContractId,
		   @valueDate as ValueDate,
		   ISNULL(@avgUpTrendFuturesRsi,0) as UpTrendRSI,
		   ISNULL(@avgDownTrendFuturesRsi,0) as DownTrendRSI

END
