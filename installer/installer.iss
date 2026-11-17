; AirBridge installer (Inno Setup 6/7). Build everything with build.ps1, or by hand:
;   engine\venv\Scripts\python.exe -m PyInstaller --noconfirm --clean AirBridgeEngine.spec   -> dist\engine\
;   dotnet publish -c Release -r win-x64 --self-contained true -o dist\app                   -> dist\app\
;   ISCC.exe installer\installer.iss                                                        -> Output\
#define MyAppName "AirBridge"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.1"
#endif
#define MyAppPublisher "Sarath"
#define MyAppURL "https://github.com/sarathlal-12/airbridge"
#define MyAppExeName "AirBridge.exe"

[Setup]
AppId={{2F2ACFD4-8EAD-431E-BFF5-40DB8225DF83}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
OutputDir=..\Output
OutputBaseFilename=AirBridge_Setup_v{#MyAppVersion}
SetupIconFile=..\assets\app_icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Admin once: AirPlay needs an inbound Windows Firewall rule, or iPhones can't connect.
PrivilegesRequired=admin
; Windows 10 2004+ (Bluetooth receiving needs it).
MinVersion=10.0.19041
CloseApplications=force
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start AirBridge when I sign in to Windows"; GroupDescription: "Options:"
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The tray app (.NET, self-contained) and the AirPlay engine (PyInstaller, one folder).
Source: "..\dist\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\engine\*"; DestDir: "{app}\engine"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Same value the tray menu's "Start with Windows" toggles.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "AirBridge"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
; Firewall: let iPhones reach the AirPlay engine on any network (Windows often marks home Wi-Fi as Public). Remove any old copy first.
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""AirBridge AirPlay"""; Flags: runhidden; StatusMsg: "Configuring Windows Firewall..."
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""AirBridge AirPlay"" dir=in action=allow program=""{app}\engine\AirBridgeEngine.exe"" enable=yes profile=any"; Flags: runhidden; StatusMsg: "Configuring Windows Firewall..."
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[UninstallRun]
; Close AirBridge (and its engine, via the job object) so its files can be removed.
Filename: "{sys}\taskkill.exe"; Parameters: "/F /T /IM AirBridge.exe"; Flags: runhidden; RunOnceId: "StopAirBridge"
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""AirBridge AirPlay"""; Flags: runhidden; RunOnceId: "RemoveFirewallRule"

[UninstallDelete]
; Per-user data (logs, settings, AirPlay pairing keys) for the user who uninstalls.
Type: filesandordirs; Name: "{localappdata}\AirBridge"

[Messages]
FinishedLabel=AirBridge is installed and runs in the system tray.%n%niPhone: open Control Center, tap the AirPlay icon on the music card and choose AirBridge.%nAndroid: pair your phone with Windows in Bluetooth settings, then in the AirBridge tray menu choose Android / Bluetooth phones > your phone > Let this phone play here.%n%nYour Wi-Fi must be set to Private (not Public) in Windows for iPhones to find AirBridge.
