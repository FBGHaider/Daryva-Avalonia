# SQLite Database Setup Guide

Step-by-step guide to set up the SQLite database for Daryva.

## Method 1: Using DB Browser for SQLite (Recommended - GUI)

### Step 1: Download DB Browser for SQLite
1. Go to https://sqlitebrowser.org/
2. Download and install DB Browser for SQLite
3. Launch the application

### Step 2: Create/Open Database
1. Click **"New Database"** button
2. Navigate to: `%AppData%\Daryva\Database\` (Windows) or `~/Library/Application Support/Daryva/Database/` (macOS)
   - **Windows**: Press `Win+R`, type `%AppData%\Daryva\Database`, press Enter
   - **macOS**: Open Finder, press `Cmd+Shift+G`, paste `~/Library/Application Support/Daryva/Database/`
3. If the folder doesn't exist, create it first
4. Name the file: `DaryvaDB.db`
5. Click **Save**

### Step 3: Run Migration Script
1. In DB Browser, click the **"Execute SQL"** tab (top toolbar)
2. Click **"Open SQL file"** button (or press `Ctrl+O` / `Cmd+O`)
3. Navigate to: `Database\Migrations\001_CreateDatabase_SQLite.sql`
4. Click **Open**
5. The SQL script will appear in the text area
6. Click the **"Execute SQL"** button (play icon) or press `F5`
7. You should see "Query executed successfully" at the bottom

### Step 4: Verify Tables Created
1. Click the **"Browse Data"** tab
2. Use the dropdown to see all tables:
   - House
   - Tenant
   - Tenancy
   - RentCharge
   - RentPayment
   - DepositPayment
   - Document
   - HouseExpense
   - Notification
   - NotificationTemplate
   - NotificationAttempt
   - AppSettings

### Step 5: Save the Database
- The database is automatically saved. You can close DB Browser.

---

## Method 2: Using Command Line (sqlite3)

### Windows

#### Step 1: Check if sqlite3 is installed
```powershell
sqlite3 --version
```

If not installed:
- Windows 10/11: sqlite3 should be available. If not, download from https://www.sqlite.org/download.html
- Or use DB Browser (Method 1)

#### Step 2: Navigate to project directory
```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva"
```

#### Step 3: Ensure database directory exists
```powershell
New-Item -ItemType Directory -Force -Path "$env:APPDATA\Daryva\Database"
```

#### Step 4: Run the migration script
```powershell
# Option A: Direct execution
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" < Database\Migrations\001_CreateDatabase_SQLite.sql

# Option B: Interactive mode (if you want to verify)
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db"
# Then in sqlite3 prompt:
.read Database/Migrations/001_CreateDatabase_SQLite.sql
.tables
.quit
```

#### Step 5: Verify tables
```powershell
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" ".tables"
```

---

### macOS / Linux

#### Step 1: Check if sqlite3 is installed
```bash
sqlite3 --version
```

If not installed:
```bash
# macOS (using Homebrew)
brew install sqlite3

# Or download from https://www.sqlite.org/download.html
```

#### Step 2: Navigate to project directory
```bash
cd ~/path/to/Daryva
```

#### Step 3: Ensure database directory exists
```bash
mkdir -p ~/Library/Application\ Support/Daryva/Database
```

#### Step 4: Run the migration script
```bash
# Option A: Direct execution
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db < Database/Migrations/001_CreateDatabase_SQLite.sql

# Option B: Interactive mode
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db
# Then in sqlite3 prompt:
.read Database/Migrations/001_CreateDatabase_SQLite.sql
.tables
.quit
```

#### Step 5: Verify tables
```bash
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db ".tables"
```

---

## Method 3: Using Visual Studio Code Extension

### Step 1: Install SQLite Extension
1. Open VS Code
2. Go to Extensions (Ctrl+Shift+X / Cmd+Shift+X)
3. Search for "SQLite" by alexcvzz
4. Install it

### Step 2: Open Database
1. Press `Ctrl+Shift+P` (Cmd+Shift+P on Mac)
2. Type "SQLite: Open Database"
3. Navigate to: `%AppData%\Daryva\Database\DaryvaDB.db` (create if doesn't exist)

### Step 3: Run Migration
1. Open the migration file: `Database\Migrations\001_CreateDatabase_SQLite.sql`
2. Right-click in the SQL editor
3. Select "Run Query" or press `Ctrl+Shift+E` (Cmd+Shift+E on Mac)
4. The script will execute against the open database

---

## Method 4: Let App Create Database, Then Run Migration

### Step 1: Run the app once
```powershell
cd Daryva-Avalonia
dotnet run
```

This will create the database file (empty) at:
- Windows: `%AppData%\Daryva\Database\DaryvaDB.db`
- macOS: `~/Library/Application Support/Daryva/Database/DaryvaDB.db`

### Step 2: Close the app

### Step 3: Run migration using any method above

---

## Quick Verification Commands

### Check if database exists
**Windows:**
```powershell
Test-Path "$env:APPDATA\Daryva\Database\DaryvaDB.db"
```

**macOS:**
```bash
test -f ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db && echo "Exists" || echo "Not found"
```

### List all tables
**Windows:**
```powershell
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" ".tables"
```

**macOS:**
```bash
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db ".tables"
```

### Check table structure
**Windows:**
```powershell
sqlite3 "$env:APPDATA\Daryva\Database\DaryvaDB.db" ".schema House"
```

**macOS:**
```bash
sqlite3 ~/Library/Application\ Support/Daryva/Database/DaryvaDB.db ".schema House"
```

---

## Troubleshooting

### "Database file is locked"
- Close any applications using the database (DB Browser, VS Code, etc.)
- Make sure the app is not running

### "No such file or directory"
- Create the directory first:
  - Windows: `New-Item -ItemType Directory -Force -Path "$env:APPDATA\Daryva\Database"`
  - macOS: `mkdir -p ~/Library/Application\ Support/Daryva/Database`

### "sqlite3: command not found"
- Install sqlite3 or use DB Browser for SQLite (Method 1)

### "Syntax error" in migration script
- Make sure you're using the SQLite version: `001_CreateDatabase_SQLite.sql`
- Not the SQL Server version: `001_CreateDatabase.sql`

---

## Recommended Approach

**For beginners:** Use **Method 1 (DB Browser for SQLite)** - it's visual and easy to use.

**For developers:** Use **Method 2 (Command Line)** - faster and scriptable.

**For VS Code users:** Use **Method 3 (VS Code Extension)** - integrated workflow.
