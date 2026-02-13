# Velopack Installer

## For GitHub updates: use `FBGHaider.Daryva-win-Setup.exe`

After running `build-win.ps1`, upload to [Daryva-Updates](https://github.com/FBGHaider/Daryva-Updates/releases).

Users who install via the Velopack-based installer get **Check for updates** in Settings → General.

## Publishing a new version (e.g. v1.0.1)

1. **Build** with the new version:
   ```powershell
   .\velopack-installer\build-win.ps1 -Version 1.0.1
   ```
2. **Create a new release** on GitHub (FBGHaider/Daryva-Updates):
   - Tag: **exactly** `v1.0.1` (with the `v` prefix).
   - Upload these files from the `releases/` folder (required for the in-app updater):
     - **`releases.win.json`** – update feed (required)
     - **`FBGHaider.Daryva-1.0.1-full.nupkg`** – update package (required)
   - Optionally also upload: `Daryva-Setup-1.0.1.exe`, `FBGHaider.Daryva-win-Setup.exe`, `RELEASES`.
3. Installed apps (e.g. on 1.0.0) will then see “Update available: 1.0.1” when they click **Check for updates**.

If the app still says “Up to date”, the update check may have failed (e.g. GitHub rate limit, or release missing the assets above). A dialog will describe possible causes.

## Build commands

**Run from the repository root** (the folder that contains `velopack-installer`), e.g. `C:\Users\Abbas Haider\Repo\Daryva`:

```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva"

# Using the root helper script (easiest):
.\build-release.ps1 -Version 1.0.3

# Or call the installer script directly:
.\velopack-installer\build-win.ps1 -Version 1.0.3

# Velopack only (no Inno Setup):
.\build-release.ps1 -Version 1.0.3 -SkipInnoSetup
```

If you get "script not recognized", make sure your current directory is the repo root (`cd` to the Daryva folder first).
