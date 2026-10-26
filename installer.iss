[Setup]
AppName=Serienummer og Vaskemærker Generator
AppVersion=1.4
DefaultDirName={autopf}\SerienummerGenerator
DefaultGroupName=Serienummer Generator
OutputDir=OutputInstaller
OutputBaseFilename=SerienummerGenerator_Setup
SetupIconFile=app.ico
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
; Hovedprogrammet
Source: "bin\Release\net10.0-windows\win-x64\publish\seriel number generator.exe"; DestDir: "{app}"; Flags: ignoreversion

[Dirs]
; Datamappe i C:\ProgramData med fuld adgang til alle brugere
Name: "{commonappdata}\SerienummerGenerator"; Permissions: users-full

; Output-mappe i app-mappen med skriverettigheder, så filer kan oprettes uden admin-krav
Name: "{app}\output"; Permissions: users-modify

[Icons]
; Genvej i Start-menuen
Name: "{group}\Serienummer Generator"; Filename: "{app}\seriel number generator.exe"
; Valgfri genvej på skrivebordet
Name: "{autodesktop}\Serienummer Generator"; Filename: "{app}\seriel number generator.exe"

[Run]
; Tilbyd brugeren at starte programmet med det samme efter installation
Filename: "{app}\seriel number generator.exe"; Description: "{cm:LaunchProgram,Serienummer Generator}"; Flags: nowait postinstall skipifsilent