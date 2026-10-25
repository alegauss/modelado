; MOD28: the per-user installer, after PhotoBoost's (build\installer.iss there). Built by
; build\build-installer.cmd, which publishes first. Every relative path is resolved against this
; file's folder (build\). Output: artifacts\installer\Modelado-Setup-<version>.exe
;
; The AppId never changes: a later installer with the same one upgrades this install in place,
; which is what the updater (MOD31) will rely on.

#define MyAppName "Modelado"
#define MyAppPublisher "Alexandre Oliveira"
#define MyAppExeName "Modelado.exe"
#define MyPublishDir "..\artifacts\publish"
; Read from the published exe, whose version comes from Directory.Build.props: one place to bump.
; ISCC /DMyAppVersion=x.y.z overrides it, which is how check-installer.ps1 builds an older one.
#ifndef MyAppVersion
  #define MyAppVersion GetStringFileInfo(MyPublishDir + "\" + MyAppExeName, PRODUCT_VERSION)
#endif

[Setup]
AppId={{B100F130-2CAB-40FB-A480-987B374E225D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Per user, no administrator: models download into the user's own folder anyway.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\Modelado
DisableProgramGroupPage=yes
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\Modelado.App\Modelado.ico
OutputDir=..\artifacts\installer
OutputBaseFilename=Modelado-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; x64 only, as the build is (Directory.Build.props), on the Windows 10 1903 the README requires.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.18362
CloseApplications=yes
RestartApplications=no
; Explorer reads the Open with list again once the install has written it.
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The app and modelado-cli, self-contained, sharing one runtime in one folder, and the licenses
; folder collect-notices.ps1 writes. No models: each operation downloads its own on first use.
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs; \
    Excludes: "*.pdb,DirectML.Debug.dll,*.lib,*.xml"

[Registry]
; "Open with Modelado" for the 3D models the app opens: the bare-path argument CommandLine.cs
; takes. Offered beside the user's default app, never replacing it.
Root: HKA; Subkey: "Software\Classes\Modelado.Model"; ValueType: string; ValueName: ""; ValueData: "3D model"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Modelado.Model\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"",0"
Root: HKA; Subkey: "Software\Classes\Modelado.Model\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""
Root: HKA; Subkey: "Software\Classes\.glb\OpenWithProgids"; ValueType: string; ValueName: "Modelado.Model"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.obj\OpenWithProgids"; ValueType: string; ValueName: "Modelado.Model"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.ply\OpenWithProgids"; ValueType: string; ValueName: "Modelado.Model"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\.stl\OpenWithProgids"; ValueType: string; ValueName: "Modelado.Model"; ValueData: ""; Flags: uninsdeletevalue
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".glb"; ValueData: ""; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".obj"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".ply"; ValueData: ""
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".stl"; ValueData: ""

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
// The models each operation downloaded live outside {app}, in %LOCALAPPDATA%\Modelado\models
// (ModelLibrary.DefaultDownloadRoot), so an upgrade never touches them. Uninstalling asks whether
// to delete them too; a silent uninstall keeps them, since they take gigabytes to fetch again.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Models: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    Models := ExpandConstant('{localappdata}\Modelado\models');
    if DirExists(Models) then
      if SuppressibleMsgBox('Delete the models Modelado downloaded too?' + #13#10 + #13#10 + Models + #13#10 + #13#10 +
          'Keep them to reinstall Modelado without downloading them again.',
          mbConfirmation, MB_YESNO or MB_DEFBUTTON2, IDNO) = IDYES then
        DelTree(Models, True, True, True);
  end;
end;
