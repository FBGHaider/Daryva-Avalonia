# Docker SQL Server Setup for Daryva

This project uses SQL Server running in a Docker container.

## Prerequisites

- Docker Desktop installed and running
- Docker Compose (included with Docker Desktop)

## Quick Start

1. **Start the SQL Server container:**
   ```bash
   docker-compose up -d
   ```

2. **Verify the container is running:**
   ```bash
   docker ps
   ```
   You should see `daryva-sqlserver` in the list.

3. **Create the database (optional - can be created on first connection):**
   
   **Option 1: Using sqlcmd (PowerShell - no -it flag needed):**
   ```powershell
   docker exec daryva-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C -Q "CREATE DATABASE DaryvaDB"
   ```
   
   **For Linux/Mac (with -it flag):**
   ```bash
   docker exec -it daryva-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C -Q "CREATE DATABASE DaryvaDB"
   ```
   
   Note: The `-C` flag trusts the server certificate (required for ODBC Driver 18).
   
   **Option 2: Using SSMS (Recommended):**
   - Open SQL Server Management Studio (SSMS)
   - Connect to: `localhost,1433`
   - Username: `sa`
   - Password: `YourStrong@Password123`
   - Right-click "Databases" → "New Database" → Name: `DaryvaDB`
   
   **Option 3: The database will be created automatically when you run the migration scripts in SSMS**

4. **Update connection string if needed:**
   - The default connection string in `App.config` uses:
     - Server: `localhost,1433`
     - Database: `DaryvaDB`
     - Username: `sa`
     - Password: `YourStrong@Password123`
   - **IMPORTANT:** Change the password in both `docker-compose.yml` and `App.config` for production use!

## Docker Commands

- **Start the SQL Server:** `docker-compose up -d`
- **Stop the SQL Server:** `docker-compose down`
- **Stop and remove volumes (deletes data):** `docker-compose down -v`
- **View logs:** `docker-compose logs -f sqlserver`
- **Access SQL Server (PowerShell):** `docker exec daryva-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C`
- **Access SQL Server (Linux/Mac):** `docker exec -it daryva-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Password123" -C`

## Connection String Details

The connection string format for Docker SQL Server:
```
Server=localhost,1433;Database=DaryvaDB;User Id=sa;Password=YourStrong@Password123;TrustServerCertificate=True;Encrypt=True;
```

**Security Note:** The default password is for development only. Please change it for production use!
