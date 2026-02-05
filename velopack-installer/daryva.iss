; Daryva - Inno Setup script (hybrid: wizard + Velopack for GitHub updates)
; Full wizard: Welcome -> License -> Options (desktop icon) -> Ready -> Installing (runs Velopack Setup) -> Finished
; Velopack installs to %LocalAppData%\FBGHaider.Daryva - enables Check for updates in Settings

#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#define MyAppName "Daryva"
#define MyAppId "FBGHaider.Daryva"
#define MyAppPublisher "FBGHaider"
#define ReleasesDir "..\releases"
#define VelopackSetup "FBGHaider.Daryva-win-Setup.exe"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppId}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\releases
OutputBaseFilename=Daryva-Setup-{#MyAppVersion}
SetupIconFile=..\Daryva-Avalonia\Assets\Logo\FBG_App_Icon_MAX.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
; No Inno uninstaller - Velopack creates its own
Uninstallable=no

; Wizard pages
DisableWelcomePage=no
; Velopack uses fixed path - no destination picker
DisableDirPage=yes
DisableReadyPage=no

LicenseFile=installer-assets\terms.rtf

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Bundle Velopack Setup.exe and dependencies - extract to temp for running
Source: "{#ReleasesDir}\{#VelopackSetup}"; DestDir: "{tmp}\velopack"; Flags: ignoreversion
Source: "{#ReleasesDir}\RELEASES"; DestDir: "{tmp}\velopack"; Flags: ignoreversion
Source: "{#ReleasesDir}\releases.win.json"; DestDir: "{tmp}\velopack"; Flags: ignoreversion
Source: "{#ReleasesDir}\FBGHaider.Daryva-{#MyAppVersion}-full.nupkg"; DestDir: "{tmp}\velopack"; Flags: ignoreversion

[Run]
; Run Velopack Setup.exe during install (enables GitHub updates)
; --silent: no full-screen splash, runs within Inno's progress
Filename: "{tmp}\velopack\{#VelopackSetup}"; Parameters: "--silent"; WorkingDir: "{tmp}\velopack"; Flags: waituntilterminated
; "Open Daryva" checkbox on completion
Filename: "{localappdata}\{#MyAppId}\Update.exe"; Parameters: "--processStart Daryva.exe"; WorkingDir: "{localappdata}\{#MyAppId}"; Description: "Open Daryva"; Flags: postinstall nowait skipifsilent

[Code]
var
  DesktopShortcutPage: TInputOptionWizardPage;
  CreateDesktopShortcut: Boolean;

procedure InitializeWizard;
begin
  DesktopShortcutPage := CreateInputOptionPage(wpLicense,
    'Additional Options', 'Choose shortcut options',
    'Daryva will be installed with GitHub update support. Select shortcut options below.',
    True, False);
  DesktopShortcutPage.Add('Create a &desktop shortcut');
  DesktopShortcutPage.Values[0] := True;
  CreateDesktopShortcut := True;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = DesktopShortcutPage.ID then
    CreateDesktopShortcut := DesktopShortcutPage.Values[0];
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  DesktopShortcutPath: String;
begin
  if CurStep = ssPostInstall then
  begin
    if not CreateDesktopShortcut then
    begin
      DesktopShortcutPath := ExpandConstant('{userdesktop}\{#MyAppName}.lnk');
      if FileExists(DesktopShortcutPath) then
        DeleteFile(DesktopShortcutPath);
    end;
  end;
end;
