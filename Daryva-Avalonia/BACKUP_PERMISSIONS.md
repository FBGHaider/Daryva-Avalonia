# SQL Server Backup Permissions Guide

## Problem
SQL Server backup fails with permission errors because the SQL Server service account doesn't have write permissions to the backup directory.

## Solution Options

### Option 1: Use Default Location (Recommended)
The application now defaults to `C:\Backups\Daryva`. This location is typically accessible to SQL Server.

1. If the folder doesn't exist, the application will create it automatically
2. If you still get permission errors, follow Option 2 below to grant permissions

### Option 2: Grant Permissions to Custom Location

If you want to use a custom backup location (e.g., `C:\Users\YourName\AppData\Roaming\Daryva\Backups`), you need to grant the SQL Server service account write permissions.

#### Steps to Grant Permissions:

1. **Find SQL Server Service Account:**
   - Open **Services** (Win + R, type `services.msc`)
   - Find **SQL Server (MSSQLSERVER)** or **SQL Server (SQLEXPRESS)**
   - Right-click → **Properties** → **Log On** tab
   - Note the account name:
     - For SQL Server: `NT SERVICE\MSSQLSERVER`
     - For SQL Server Express: `NT SERVICE\MSSQL$SQLEXPRI` (or similar)
     - May also be `NT AUTHORITY\SYSTEM` or `Local System`

2. **Grant Folder Permissions:**
   - Navigate to your backup folder (e.g., `C:\Backups\Daryva` or `C:\Users\YourName\AppData\Roaming\Daryva\Backups`)
   - Right-click the folder → **Properties** → **Security** tab
   - Click **Edit** → **Add**
   - Enter the SQL Server service account name:
     - For SQL Server: `NT SERVICE\MSSQLSERVER`
     - For SQL Server Express: `NT SERVICE\MSSQL$SQLEXPRI`
   - Click **Check Names** to verify (it should underline the name)
   - Click **OK**
   - Select the account → Check **Full control** or at least **Modify** and **Write**
   - Click **OK** to apply
   
   **Note:** If you can't find the folder or it doesn't exist, create it first, then grant permissions.

3. **Alternative: Use a Shared Location**
   - Create a folder like `C:\Backups\Daryva`
   - Grant the SQL Server service account full control
   - Use this location in the backup settings

### Option 3: Use SQL Server Default Backup Location

SQL Server has a default backup location that it always has access to:
- Usually: `C:\Program Files\Microsoft SQL Server\MSSQL15.MSSQLSERVER\MSSQL\Backup`
- Or: `C:\Program Files\Microsoft SQL Server\MSSQL16.MSSQLSERVER\MSSQL\Backup` (for SQL Server 2022)

**Note:** The exact path depends on your SQL Server version and instance name.

### For Docker SQL Server

If you're using SQL Server in Docker:
- The backup path must be inside the container or a mounted volume
- Use paths like `/var/opt/mssql/backup/` inside the container
- Or mount a host directory to the container and use that path

## Troubleshooting

### Error: "SQL Server does not have permission to write"
- Verify the SQL Server service account has write permissions
- Try using `C:\Backups\Daryva` as the backup location
- Check Windows Event Viewer for detailed error messages

### Error: "Backup file was not found"
- SQL Server executed the backup but couldn't write the file
- Check disk space
- Verify the path is correct and accessible
- Ensure SQL Server service account has permissions

### Still Having Issues?
1. Use the default location `C:\Backups\Daryva`
2. If that doesn't work, check Windows Event Viewer for SQL Server errors
3. Consider using SQL Server Management Studio (SSMS) to test backup manually
