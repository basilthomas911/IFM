-- =============================================
-- Author:		Basil Thomas	
-- Create date: 2022-02-02
-- Description:	return single fund
-- =============================================
CREATE PROCEDURE [dbo].[spGetFund] 
@fundId as int	
AS
BEGIN
	-- SET NOCOUNT ON added to prevent extra result sets from
	-- interfering with SELECT statements.
	SET NOCOUNT ON;

    -- Insert statements for procedure here
	SELECT [FundId]
      ,[Name]
      ,[Description]
	  ,[Balance]
      ,[CreatedOn]
      ,[CreatedBy]
	FROM [dbo].[fund]
	where FundId = @fundId

END
