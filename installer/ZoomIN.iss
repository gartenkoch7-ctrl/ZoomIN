; ZoomIN installer (ADR-0009): per user, no admin rights, x64 only.
; Built by scripts/publish.ps1 with /DAppVersion, /DSourceDir and /DOutputDir.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\ZoomIN"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{8C6F0E5A-2B7D-4F3B-9C1E-5A7D2E4B6F10}
AppName=ZoomIN
AppVersion={#AppVersion}
AppPublisher=ZoomIN contributors
AppComments=Kostenlose Open-Source-Bildschirmlupe / free open-source screen magnifier
DefaultDirName={autopf}\ZoomIN
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputDir}
OutputBaseFilename=ZoomIN-{#AppVersion}-setup
SetupIconFile=..\src\ZoomIN.App\Assets\ZoomIN.ico
UninstallDisplayIcon={app}\ZoomIN.exe
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes

[Languages]
Name: "de"; MessagesFile: "compiler:Languages\German.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
de.AutostartTask=ZoomIN mit Windows starten
en.AutostartTask=Start ZoomIN with Windows

[Tasks]
; The app's first-run assistant asks again; this only presets it.
Name: "autostart"; Description: "{cm:AutostartTask}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\ZoomIN"; Filename: "{app}\ZoomIN.exe"

[Registry]
; Same value the app writes (RegistryAutostart), so both stay in sync.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ZoomIN"; \
  ValueData: """{app}\ZoomIN.exe"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\ZoomIN.exe"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Ends ZoomIN and its watchdog together (same image name), so nothing restarts during uninstall.
Filename: "{sys}\taskkill.exe"; Parameters: "/IM ZoomIN.exe /F"; Flags: runhidden; RunOnceId: "StopZoomIN"
Filename: "{sys}\reg.exe"; Parameters: "delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v ZoomIN /f"; \
  Flags: runhidden; RunOnceId: "RemoveAutostart"
