# Cross-Platform Migration Summary

## Overview

Daryva has been migrated from Windows-only to cross-platform (Windows, macOS, Linux). This document summarizes all changes made.

## Files Created

### Platform Abstractions
1. **`Daryva-Avalonia/Services/Platform/IAppPaths.cs`** - Interface for application paths
2. **`Daryva-Avalonia/Services/Platform/AppPaths.cs`** - Cross-platform path implementation
3. **`Daryva-Avalonia/Services/Platform/ISecureStore.cs`** - Interface for secure storage
4. **`Daryva-Avalonia/Services/Platform/SecureStore.cs`** - Cross-platform secure storage (DPAPI on Windows, AES on macOS/Linux)

### Documentation
1. **`MACOS_MIGRATION.md`** - Detailed migration guide
2. **`MACOS_TEST_CHECKLIST.md`** - Testing checklist for macOS
3. **`CROSS_PLATFORM_SUMMARY.md`** - This file

## Files Modified

### Core Services
1. **`Daryva-Avalonia/Services/ConfigurationService.cs`**
   - Removed: `System.Configuration.ConfigurationManager` (Windows-only)
   - Added: JSON-based configuration files
   - Default connection string now uses SQLite

2. **`Daryva-Avalonia/Services/Database/DbContext.cs`**
   - Removed: `Microsoft.Data.SqlClient`
   - Added: `Microsoft.Data.Sqlite`
   - Updated connection handling for SQLite

3. **`Daryva-Avalonia/Services/Business/BackupService.cs`**
   - Removed: Windows-only SQL Server backup paths
   - Added: SQLite file copy backup
   - Uses `IAppPaths` for cross-platform paths

4. **`Daryva-Avalonia/Services/ServiceCollectionExtensions.cs`**
   - Added: Registration for `IAppPaths` and `ISecureStore`

### Project Configuration
1. **`Daryva-Avalonia/Daryva.csproj`**
   - Changed: `OutputType` from `WinExe` to `Exe`
   - Removed: `System.Configuration.ConfigurationManager`
   - Removed: `Microsoft.Data.SqlClient`
   - Added: `Microsoft.Data.Sqlite`
   - Updated: Platform-specific Avalonia packages with conditions

2. **`Daryva-Avalonia/App.config`**
   - Updated: Comments to reflect SQLite default

## Key Changes

### 1. Path Management
- All hardcoded Windows paths removed
- Uses `Environment.SpecialFolder` for cross-platform paths
- `IAppPaths` provides consistent path access

### 2. Configuration
- Moved from XML `App.config` to JSON `app.config.json`
- Configuration stored in `AppData/Daryva/`
- Local overrides in `app.config.local.json`

### 3. Database
- Migrated from SQL Server to SQLite
- Database file: `AppData/Daryva/Database/DaryvaDB.db`
- **Note**: SQL queries in repositories need SQLite syntax updates

### 4. Secure Storage
- Windows: DPAPI (ProtectedData)
- macOS/Linux: AES encryption with user key file
- **TODO**: macOS Keychain integration

## Remaining Work

### High Priority
1. ~~**SQL Syntax Updates**: Update all repositories to use SQLite syntax~~ ✅ **DONE**
   - All repositories use `last_insert_rowid()`, `datetime('now')`, `COALESCE`, `LIMIT 1`
   - SettingsService.GetDatabaseSizeAsync: Removed SQL Server-specific code (sys.master_files, DB_ID())

2. **Database Migrations**: Convert SQL Server migration scripts to SQLite (001_CreateDatabase_SQLite.sql already exists)

### Medium Priority
1. **macOS Keychain**: Integrate Keychain for SecureStore on macOS
2. **Testing**: Comprehensive testing on macOS
3. **Documentation**: Update user documentation for macOS

### Low Priority
1. **Linux Support**: Test and document Linux support
2. **CI/CD**: Add macOS build to CI/CD pipeline

## Testing Commands

### macOS Build & Run
```bash
cd Daryva-Avalonia
dotnet restore
dotnet build
dotnet run
```

### macOS Publish
```bash
# Apple Silicon
dotnet publish -c Release -r osx-arm64 --self-contained

# Intel
dotnet publish -c Release -r osx-x64 --self-contained
```

## File Locations

### Windows
- App Data: `%AppData%\Daryva\`
- Database: `%AppData%\Daryva\Database\DaryvaDB.db`
- Exports: `%UserProfile%\Documents\Daryva Exports\`

### macOS
- App Data: `~/Library/Application Support/Daryva/`
- Database: `~/Library/Application Support/Daryva/Database/DaryvaDB.db`
- Exports: `~/Documents/Daryva Exports/`

### Linux
- App Data: `~/.config/Daryva/`
- Database: `~/.config/Daryva/Database/DaryvaDB.db`
- Exports: `~/Documents/Daryva Exports/`

## Breaking Changes

1. **Configuration Files**: Old `App.config` XML format no longer used. Migrate to JSON format.
2. **Database**: SQL Server connection strings no longer work. Use SQLite connection strings.
3. **Backup Format**: SQL Server `.bak` files replaced with SQLite `.db` file copies.

## Migration Path

1. ✅ Platform abstractions created
2. ✅ Configuration service updated
3. ✅ Database context migrated to SQLite
4. ✅ Backup service updated
5. ✅ Project file updated
6. ✅ SQL queries updated to SQLite syntax (all repositories + SettingsService)
7. ⏳ Database migrations need conversion (001_CreateDatabase_SQLite.sql exists)
8. ⏳ Testing on macOS

## Notes

- Program.cs already uses `StartWithClassicDesktopLifetime` (good for cross-platform)
- Avalonia UI framework is cross-platform by design
- Dapper works with both SQL Server and SQLite (just need SQL syntax updates)
- File operations use .NET `Path` class (cross-platform)
