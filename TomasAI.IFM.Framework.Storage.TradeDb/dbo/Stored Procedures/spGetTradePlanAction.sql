-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spGetTradePlanAction] 
	-- Add the parameters for the stored procedure here
	@valueDate date,
	@orderId int,
	@tradeId int
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

	if (exists(select * from trade_plan_action where OrderId = @orderId	and TradeId = @tradeId and ValueDate = @valueDate))
		-- Insert statements for procedure here
		select 
			tp.TradePlanId,
			tp.OrderId,
			tp.TradeId,
			tp.ValueDate,
			tpa.ActionDate,
			tpa.ActionType,
			tpa.ActionSubType,
			tpa.ActionState,
			tpa.ActionReason,
			tp.MarketTrend,
			tp.MarketVolatility,
			tp.MarketDirection,
			tp.VixVolatility,
			tp.TradeRisk,
			tp.GammaRisk,
			tp.TradePnl,
			tp.ForwardLossRatio,
			tp.MScore,
			tp.NetPrice,
			tp.ForwardPrice,
			tp.StopLossLimit,
			tp.CreatedOn,
			tp.CreatedBy
		from trade_plan_action tpa
		join trade_plan tp on tpa.TradePlanId = tp.TradePlanId
		where tpa.OrderId = @orderId
		and tpa.TradeId = @tradeId
		and tpa.ValueDate = @valueDate 
		order by tpa.ActionDate desc
	else
		begin
			declare @prevValueDate date
			select @prevValueDate = max(ValueDate) from trade_plan_action where OrderId = @orderId	and TradeId = @tradeId and ValueDate < @valueDate
			select 
				tp.TradePlanId,
				tp.OrderId,
				tp.TradeId,
				tp.ValueDate,
				tpa.ActionDate,
				tpa.ActionType,
				tpa.ActionSubType,
				tpa.ActionState,
				tpa.ActionReason,
				tp.MarketTrend,
				tp.MarketVolatility,
				tp.MarketDirection,
				tp.VixVolatility,
				tp.TradeRisk,
				tp.GammaRisk,
				tp.TradePnl,
				tp.ForwardLossRatio,
				tp.MScore,
				tp.NetPrice,
				tp.ForwardPrice,
				tp.StopLossLimit,
				tp.CreatedOn,
				tp.CreatedBy
			from trade_plan_action tpa
			join trade_plan tp on tpa.TradePlanId = tp.TradePlanId
			where tpa.OrderId = @orderId
			and tpa.TradeId = @tradeId
			and tpa.ValueDate = @prevValueDate 
			order by tpa.ActionDate desc
		end


END
