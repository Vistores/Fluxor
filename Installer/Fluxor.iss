#define MyAppName "Fluxor"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Dokzya_dev"
#define MyAppExeName "Fluxor.exe"
#define RepoRoot ".."
#define PublishDir "..\CursorFX.App\bin\Release\net9.0-windows"
#define PluginsDir "..\Plugins"
#define InstallerOutput "..\artifacts\installer"

[Setup]
AppId={{E4762A39-4D34-4DC1-9729-3BE0F3B4A1B4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=no
AllowNoIcons=yes
PrivilegesRequired=lowest
OutputDir={#InstallerOutput}
OutputBaseFilename=Fluxor-Setup-v{#MyAppVersion}
SetupIconFile={#RepoRoot}\CursorFX.App\Assets\FluxorIco.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startmenuicon"; Description: "Create Start Menu shortcuts"; GroupDescription: "Additional shortcuts:"; Flags: checkedonce

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "{#PluginsDir}\README.md"; DestDir: "{userdocs}\Fluxor\Plugins"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginCursorFlameContour\CursorFlameContourPlugin.cs"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginCursorFlameContour"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginCursorFlameContour\Fluxor.PluginCursorFlameContour.csproj"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginCursorFlameContour"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginVelvetVoid\VelvetVoidPlugin.cs"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginVelvetVoid"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginVelvetVoid\Fluxor.PluginVelvetVoid.csproj"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginVelvetVoid"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginFireflySwarm\FireflySwarmPlugin.cs"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginFireflySwarm"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginFireflySwarm\Fluxor.PluginFireflySwarm.csproj"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginFireflySwarm"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginRetroTrace\RetroTracePlugin.cs"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginRetroTrace"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginRetroTrace\Fluxor.PluginRetroTrace.csproj"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginRetroTrace"; Flags: ignoreversion

Source: "{#PluginsDir}\Fluxor.PluginCursorFlameContour\bin\Release\net9.0-windows\Fluxor.PluginCursorFlameContour.dll"; DestDir: "{userdocs}\Fluxor\Plugins\ReadyToImport"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginVelvetVoid\bin\Release\net9.0-windows\Fluxor.PluginVelvetVoid.dll"; DestDir: "{userdocs}\Fluxor\Plugins\ReadyToImport"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginFireflySwarm\bin\Release\net9.0-windows\Fluxor.PluginFireflySwarm.dll"; DestDir: "{userdocs}\Fluxor\Plugins\ReadyToImport"; Flags: ignoreversion
Source: "{#PluginsDir}\Fluxor.PluginRetroTrace\bin\Release\net9.0-windows\Fluxor.PluginRetroTrace.dll"; DestDir: "{userdocs}\Fluxor\Plugins\ReadyToImport"; Flags: ignoreversion

Source: "{#PublishDir}\CursorFX.Core.dll"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginSdk"; Flags: ignoreversion
Source: "{#RepoRoot}\CursorFX.App\Assets\FluxorIco.ico"; DestDir: "{userdocs}\Fluxor\Plugins\Fluxor.PluginSdk"; Flags: ignoreversion
Source: "{#RepoRoot}\Installer\Prereqs\windowsdesktop-runtime-9.0.14-win-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startmenuicon
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\windowsdesktop-runtime-9.0.14-win-x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft .NET 9 Desktop Runtime..."; Flags: waituntilterminated skipifsilent; Check: not HasDesktopRuntime
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function HasRuntimeInDir(BaseDir: string): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  if not DirExists(BaseDir) then
    exit;

  if FindFirst(AddBackslash(BaseDir) + '*', FindRec) then
  begin
    try
      repeat
        if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
           (FindRec.Name <> '.') and
           (FindRec.Name <> '..') and
           (Pos('9.', FindRec.Name) = 1) then
        begin
          Result := True;
          break;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function HasDesktopRuntime: Boolean;
begin
  Result :=
    HasRuntimeInDir(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App')) or
    HasRuntimeInDir(ExpandConstant('{commonpf32}\dotnet\shared\Microsoft.WindowsDesktop.App'));
end;
