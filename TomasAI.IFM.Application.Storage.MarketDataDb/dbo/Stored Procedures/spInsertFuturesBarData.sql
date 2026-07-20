-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFuturesBarData] 
	-- Add the parameters for the stored procedure here
	@contractId varchar(64),
	@symbol varchar(64),
	@valueDate date,
	@barDate datetime,
	@barRateType varchar(50),
	@barValue money,
	@upTrendTrigger real,
	@downTrendTrigger real
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	delete 
	from futures_bar_data
	where ContractId = @contractId
	and Symbol = @symbol
	and ValueDate = @valueDate
	and BarDate = @barDate

    -- Insert statements for procedure here
	insert into futures_bar_data(
		ContractId,
		Symbol,
		ValueDate,
		BarDate,
		BarRateType,
		BarValue,
		UpTrendTrigger,
		DownTrendTrigger
	) values (
		@contractId,
		@symbol,
		@valueDate,
		@barDate,
		@barRateType,
		@barValue,
		@upTrendTrigger,
		@downTrendTrigger
	)

END
