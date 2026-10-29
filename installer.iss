#ifndef MyAppVersion
#define MyAppVersion "1.2.0"
#endif

[Setup]
AppName=Serienummer og Vaskemærker Generator
AppVersion={#MyAppVersion}

; Sætter versionsnummeret direkte ind i filnavnet (f.eks. SerienummerGenerator_Setup_v1.2.0.exe)
OutputBaseFilename=SerienummerGenerator_Setup_v{#MyAppVersion}

; Sætter versionsnummer i Windows-filens egenskaber/detaljer (Højreklik -> Egenskaber -> Detaljer)
VersionInfoVersion={#MyAppVersion}
VersionInfoTextVersion={#MyAppVersion}

DefaultDirName={autopf}\SerienummerGenerator
DefaultGroupName=Serienummer Generator
OutputDir=OutputInstaller
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