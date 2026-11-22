; CATTECH OPTIMIZER PRO - instalador para Windows (Inno Setup 7)
; Compilar con: ISCC.exe CATTECH-Optimizer-Pro.iss
; Requiere que el payload exista en ..\output\installer-payload\win-x64
; (ver installer\README.md para regenerarlo).

#define MyAppName "CATTECH OPTIMIZER PRO"
#define MyAppVersion "0.3.0"
#define MyAppPublisher "CATTECH"
#define MyAppExeName "Cattech.Optimizer.Pro.UI.exe"
#define MyAppUrl "https://github.com/tulatraes25/CATTECH-OPTIMIZER-PRO"
#define MyPayloadDir "..\output\installer-payload\win-x64"

[Setup]
AppId={{8F3C2A6B-4D1E-4B7A-9C55-2E9A1F0C7D31}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
InfoAfterFile=LEEME-INSTALACION.txt
OutputDir=..\output\installer
OutputBaseFilename=CATTECH-Optimizer-Pro-v{#MyAppVersion}-Setup
SetupIconFile=app.ico
UninstallDisplayIcon={app}\CATTECH.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=CATTECH OPTIMIZER PRO - Instalador
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}
VersionInfoCopyright=MIT License - CATTECH
AllowNoIcons=yes
CloseApplications=yes
RestartApplications=no
DisableWelcomePage=no
SetupLogging=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; La aplicacion escribe data\, reports\ y backups\ junto al EXE:
; instalar en una carpeta escribible por el usuario que la usa.
Source: "{#MyPayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "app.ico"; DestDir: "{app}"; DestName: "CATTECH.ico"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\CATTECH.ico"; Comment: "Diagnóstico, optimización y reparación de Windows"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\CATTECH.ico"; Tasks: desktopicon

[Run]
; Se lanza sin elevar: el tecnico debe elegir "Ejecutar como administrador" para el diagnostico completo.
Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Se conserva a proposito el historial de trabajo en {app}\data, {app}\reports y {app}\backups.
; Solo se eliminan restos temporales del propio programa.
Type: filesandordirs; Name: "{app}\logs"
