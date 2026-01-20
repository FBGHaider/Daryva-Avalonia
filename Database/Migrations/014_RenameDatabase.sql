-- =============================================
-- Rename Database from FBGRentoraDB to DaryvaDB
-- =============================================
-- This script will:
-- 1. Set the database to SINGLE_USER mode (closes all connections)
-- 2. Rename the database
-- 3. Set it back to MULTI_USER mode
-- =============================================

-- Step 1: Close all active connections and set to SINGLE_USER mode
USE master;
GO

-- Kill all active connections to the database
DECLARE @kill varchar(8000) = '';
SELECT @kill = @kill + 'KILL ' + CONVERT(varchar(5), session_id) + ';'
FROM sys.dm_exec_sessions
WHERE database_id = DB_ID('FBGRentoraDB')
AND session_id <> @@SPID;

EXEC(@kill);
GO

-- Set database to SINGLE_USER mode (this will close any remaining connections)
ALTER DATABASE FBGRentoraDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
GO

-- Step 2: Rename the database
ALTER DATABASE FBGRentoraDB MODIFY NAME = DaryvaDB;
GO

-- Step 3: Set back to MULTI_USER mode
ALTER DATABASE DaryvaDB SET MULTI_USER;
GO

PRINT 'Database successfully renamed from FBGRentoraDB to DaryvaDB';
GO
