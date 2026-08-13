#define MyAppName "QuickConvert"
#define MyAppVersion "0.2.2"
#define MyAppPublisher "QuickConvert contributors"
#define MyAppExeName "QuickConvert.exe"

[Setup]
AppId={{EB3F796C-7AB3-4DB6-B890-28B624A89E40}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\QuickConvert
DefaultGroupName=QuickConvert
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\branding\quickconvert.ico
WizardSmallImageFile=..\assets\branding\quickconvert-wizard-small.png
OutputDir=..\artifacts\installer
OutputBaseFilename=QuickConvert-{#MyAppVersion}-win-x64-setup
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\extensions\chrome\*"; DestDir: "{app}\extensions\chrome"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\extensions\quickconvert-firefox.xpi"; DestDir: "{app}\extensions"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\QuickConvert"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Rozszerzenie Chrome"; Filename: "{app}\extensions\chrome"
Name: "{group}\Rozszerzenie Firefox"; Filename: "{app}\extensions\quickconvert-firefox.xpi"

[Registry]
Root: HKCU; Subkey: "Software\Classes\*\shell\QuickConvert"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Konwertuj…"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\QuickConvert"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"
Root: HKCU; Subkey: "Software\Classes\*\shell\QuickConvert"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKCU; Subkey: "Software\Classes\*\shell\QuickConvert\command"; ValueType: string; ValueData: """{app}\{#MyAppExeName}"" --shell ""%1"""
Root: HKCU; Subkey: "Software\Google\Chrome\NativeMessagingHosts\com.quickconvert.app"; ValueType: string; ValueData: "{app}\native\chrome.json"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Mozilla\NativeMessagingHosts\com.quickconvert.app"; ValueType: string; ValueData: "{app}\native\firefox.json"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Uruchom QuickConvert"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\native"

[Code]
function JsonPath(const Value: String): String;
begin
  Result := Value;
  StringChangeEx(Result, '\', '\\', True);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  NativeDirectory: String;
  HostPath: String;
  ChromeManifest: String;
  FirefoxManifest: String;
begin
  if CurStep <> ssPostInstall then
    Exit;
  NativeDirectory := ExpandConstant('{app}\native');
  ForceDirectories(NativeDirectory);
  HostPath := JsonPath(ExpandConstant('{app}\QuickConvert.NativeHost.exe'));
  ChromeManifest := '{"name":"com.quickconvert.app","description":"QuickConvert Native Host","path":"' + HostPath + '","type":"stdio","allowed_origins":["chrome-extension://abpjmchafogplinlgklgfoljglakhalp/"]}';
  FirefoxManifest := '{"name":"com.quickconvert.app","description":"QuickConvert Native Host","path":"' + HostPath + '","type":"stdio","allowed_extensions":["quickconvert@local"]}';
  SaveStringToFile(NativeDirectory + '\chrome.json', ChromeManifest, False);
  SaveStringToFile(NativeDirectory + '\firefox.json', FirefoxManifest, False);
end;
