#define AppName "cccalendar"
#define AppVersion "0.6.8"
#define AppPublisher "cccalendar"
#define AppExecutable "cccalendar.exe"

[Setup]
AppId={{2C8BE87A-FA5B-4ECA-BF71-45A176679177}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=cccalendar-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\src\CcCalendar.Desktop\Assets\cccalendar.ico
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExecutable}
CloseApplications=yes
RestartApplications=no

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExecutable}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExecutable}"

[Run]
Filename: "{app}\{#AppExecutable}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
