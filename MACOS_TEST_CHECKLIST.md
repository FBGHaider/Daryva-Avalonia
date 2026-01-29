# macOS Test Checklist

Quick reference for testing Daryva on macOS.

## Prerequisites

- [ ] .NET 8 SDK installed (`dotnet --version` shows 8.x)
- [ ] Repository cloned/checked out
- [ ] Terminal access

## Build & Run

### Basic Test

```bash
cd Daryva-Avalonia
dotnet restore
dotnet build
dotnet run
```

**Expected**: App window opens without errors.

### Publish Test

```bash
# For Apple Silicon (M1/M2/M3)
dotnet publish -c Release -r osx-arm64 --self-contained

# For Intel Macs
dotnet publish -c Release -r osx-x64 --self-contained
```

**Expected**: Creates `bin/Release/net8.0/osx-arm64/publish/` (or `osx-x64`) with executable.

### Run Published App

```bash
cd bin/Release/net8.0/osx-arm64/publish
./Daryva-Avalonia
```

**Note**: If Gatekeeper blocks it:
1. Right-click → **Open**
2. Or: System Settings → Privacy & Security → **Open Anyway**

## Functional Tests

### Application Startup
- [ ] App launches without errors
- [ ] Main window displays correctly
- [ ] No console errors

### Paths & Storage
- [ ] App data directory created: `~/Library/Application Support/Daryva/`
- [ ] Database directory created: `~/Library/Application Support/Daryva/Database/`
- [ ] Logs directory created: `~/Library/Application Support/Daryva/Logs/`
- [ ] Exports directory created: `~/Documents/Daryva Exports/`

### Database
- [ ] Database file created (or connection works if DB exists)
- [ ] Can query data (if tables exist)
- [ ] Can insert/update data (if tables exist)

**Note**: You may need to run database migrations first. See `MACOS_MIGRATION.md`.

### File Operations
- [ ] Open file dialog works
- [ ] Save file dialog works
- [ ] Folder browser dialog works
- [ ] Document storage path is accessible

### Settings
- [ ] Settings can be saved
- [ ] Settings persist after app restart
- [ ] Config files created in correct location

### UI Features
- [ ] Navigation between views works
- [ ] Dialogs open/close correctly
- [ ] Theme switching works (if implemented)
- [ ] Data grids display correctly

### Exports
- [ ] Export to Excel works
- [ ] Files saved to `~/Documents/Daryva Exports/` (or user-selected location)

### Backups
- [ ] Backup creation works (SQLite file copy)
- [ ] Backup saved to `~/Library/Application Support/Daryva/Backups/`

## Known Issues to Check

- [ ] SQL syntax errors (if repositories not yet updated)
- [ ] File permission issues
- [ ] Path separator issues (should be handled by .NET)
- [ ] Case sensitivity (macOS is case-insensitive by default, but be aware)

## Troubleshooting

### App won't start
```bash
# Check for errors
dotnet run 2>&1 | tee run.log

# Check .NET version
dotnet --version

# Check if Avalonia packages restored
dotnet list package
```

### Database errors
- Check connection string in `~/Library/Application Support/Daryva/app.config.json`
- Verify database file exists and is writable
- Check SQL syntax (may need SQLite-compatible queries)

### File permission errors
```bash
# Check directory permissions
ls -la ~/Library/Application\ Support/Daryva/

# Fix if needed (be careful!)
chmod -R u+w ~/Library/Application\ Support/Daryva/
```

### Gatekeeper issues
- Right-click app → **Open** (first time only)
- Or: System Settings → Privacy & Security → **Open Anyway**

## Success Criteria

✅ App builds without errors  
✅ App runs and displays UI  
✅ No Windows-specific API errors  
✅ Paths resolve correctly on macOS  
✅ Database operations work (after SQL updates)  
✅ File operations work  
✅ Settings persist  

## Next Steps After Testing

1. Fix any SQL syntax issues in repositories
2. Test all features end-to-end
3. Update documentation
4. Consider CI/CD for macOS builds
