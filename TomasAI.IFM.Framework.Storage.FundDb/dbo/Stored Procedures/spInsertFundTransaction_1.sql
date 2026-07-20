-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spInsertFundTransaction] 
	-- Add the parameters for the stored procedure here
	@transactionDate datetime,
	@transactionType varchar(32),
	@fundId int,
	@orderId int,
	@tradeId int,
	@tradeType varchar(64),
	@valueDate date,
	@tradeStatus varchar(32),
	@description varchar(4000) = null,
	@amount money,
	@balance money
AS
BEGIN

	insert into dbo.fund_transaction(
	    TransactionDate,
		TransactionType,
		FundId,
		OrderId,
		TradeId,
		TradeType,
		ValueDate,
		TradeStatus,
		Description,
		Amount,
		Balance
	) values (
		@transactionDate,
		@transactionType,
		@fundId,
		@orderId,
		@tradeId,
		@tradeType,
		@valueDate,
		@tradeStatus,
		@description,
		@amount,
		@balance
	)

	

END
