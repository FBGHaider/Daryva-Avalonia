# Velopack Installer

## For GitHub updates: use `FBGHaider.Daryva-win-Setup.exe`

After running `build-win.ps1`, upload to [Daryva-Updates](https://github.com/FBGHaider/Daryva-Updates/releases):

- `FBGHaider.Daryva-win-Setup.exe` (main installer)
- `RELEASES`
- `releases.win.json`
- `FBGHaider.Daryva-{version}-full.nupkg`

Users who install via `FBGHaider.Daryva-win-Setup.exe` get **Check for updates** in Settings → General.

## Build commands

```powershell
# Full build (Velopack + Inno Setup)
.\velopack-installer\build-win.ps1 -Version 1.0.0

# Velopack only (recommended for update feed)
.\velopack-installer\build-win.ps1 -Version 1.0.0 -SkipInnoSetup
```
