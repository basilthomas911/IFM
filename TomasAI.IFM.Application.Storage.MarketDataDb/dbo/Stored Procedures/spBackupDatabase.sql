-- =============================================
-- Author:	Basil Thomas
-- Create date: 2019-03-17
-- Description:	backup market data database
-- =============================================
CREATE PROCEDURE [dbo].[spBackupDatabase]
	-- Add the parameters for the stored procedure here
	@backupType char(4)
AS
BEGIN
	if @backupType = 'full'
		BACKUP DATABASE [marketdatadb] TO  disk = 'e:\backup\marketdatadb-full.bak' WITH  FORMAT, INIT, SKIP, NOREWIND, NOUNLOAD,  STATS = 10
	else
		BACKUP DATABASE [marketdatadb] TO  disk = 'e:\backup\marketdatadb-diff.bak' WITH  DIFFERENTIAL , FORMAT, INIT,  SKIP, NOREWIND, NOUNLOAD,  STATS = 10
END