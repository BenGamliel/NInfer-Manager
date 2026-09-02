#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName "NInfer Manager"
#define ProjectRoot ".."

[Setup]
AppId={{A95A7EBC-0502-4B19-B94F-8F6CA39C461E}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=NInfer Manager contributors
DefaultDirName={localappdata}\Programs\NInfer Manager
DefaultGroupName=NInfer Manager
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#ProjectRoot}\dist\Installer
OutputBaseFilename=NInfer-Manager-Setup-{#AppVersion}
SetupIconFile={#ProjectRoot}\src\NInferManager\Assets\ninfer-manager.ico
UninstallDisplayIcon={app}\NInfer Manager.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile={#ProjectRoot}\LICENSE
InfoAfterFile={#ProjectRoot}\docs\USER_GUIDE.md
VersionInfoVersion={#AppVersion}
VersionInfoProductName=NInfer Manager
VersionInfoDescription=Unofficial lightweight GUI and model manager for NInfer

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked
Name: "startup"; Description: "Start NInfer Manager with Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "{#ProjectRoot}\build\installed-payload\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{localappdata}\NInfer Manager\Data"; Flags: uninsneveruninstall
Name: "{localappdata}\NInfer Manager\Models"; Flags: uninsneveruninstall

[Icons]
Name: "{group}\NInfer Manager"; Filename: "{app}\NInfer Manager.exe"
Name: "{group}\Uninstall NInfer Manager"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NInfer Manager"; Filename: "{app}\NInfer Manager.exe"; Tasks: desktopicon
Name: "{userstartup}\NInfer Manager"; Filename: "{app}\NInfer Manager.exe"; Parameters: "--minimized"; Tasks: startup

[Run]
Filename: "{app}\NInfer Manager.exe"; Description: "Launch NInfer Manager"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
