; Installer for the EmpiFis JSON service, with ReceiptTester as an optional component.
; Build it with installer\build-installer.ps1, which publishes both apps and passes:
;   /DAppVersion=2.3.2 /DServiceDir=<Till publish folder> /DTesterExe=<ReceiptTester.exe> /DManualFile=<docx name>
;
; Works for a fresh PC and for updating a PC where the service was only unzipped: it stops the running
; service, removes files of older builds, keeps config.json, and registers the service like
; deploy\install-and-update.bat. ReceiptTester and automatic start are unticked by default; on an update the
; autostart checkbox shows the service's current start type (a silent update without /TASKS keeps it).

#ifndef AppVersion
  #error Build with installer\build-installer.ps1 (AppVersion, ServiceDir, TesterExe, ManualFile)
#endif

#define ServiceName "empifisJsonAPI2Service"
#define ServiceDisplayName "EmpiFis JSON API 2"
#define ServiceExe "empifisJsonService2.exe"
#define ServiceFolder "C:\Altera\EmpifisJsonAPI"
#define TesterFolder "C:\Altera\ReceiptTester"
#define LogFolder "C:\Altera\Log"
#define FirewallRule "EmpiFis JSON API 2"
#define EmpiFisXClsid "{AB882ADA-330A-4656-B401-76DDD7F68D08}"

[Setup]
AppId={{409FA52D-485E-4778-A2FB-B0A14D89A458}
AppName=EmpiFis JSON Service
AppVersion={#AppVersion}
AppVerName=EmpiFis JSON Service {#AppVersion}
DefaultDirName={#ServiceFolder}
DisableDirPage=yes
DefaultGroupName=EmpiFis JSON
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x86compatible
OutputDir=output
OutputBaseFilename=EmpifisJsonSetup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
UninstallDisplayName=EmpiFis JSON Service
UninstallDisplayIcon={app}\{#ServiceExe}
; The service and running apps are stopped in [Code] (PrepareToInstall).
CloseApplications=no
; Don't reuse the previous run's choices: ReceiptTester must start unticked every time, and the
; autostart checkbox is set from the service's current start type in InitializeWizard.
UsePreviousSetupType=no
UsePreviousTasks=no

[Types]
Name: "standard"; Description: "EmpiFis JSON service"
Name: "full"; Description: "EmpiFis JSON service and ReceiptTester"
Name: "custom"; Description: "Custom"; Flags: iscustom

[Components]
Name: "service"; Description: "EmpiFis JSON service ({#ServiceFolder})"; Types: standard full custom; Flags: fixed
Name: "tester"; Description: "ReceiptTester test tool ({#TesterFolder}) - prints real receipts and Z reports, not for customer tills"; Types: full

[Tasks]
; Unticked for a new install; on an update it shows the service's current start type (InitializeWizard).
Name: "autostart"; Description: "Start the service automatically with Windows (needed on a till)"; Flags: unchecked
Name: "firewall"; Description: "Allow the POS on other computers to connect (Windows Firewall rule for the service port)"

[Dirs]
Name: "{#LogFolder}"; Permissions: users-modify; Flags: uninsneveruninstall
Name: "{#LogFolder}\Archive"; Permissions: users-modify; Flags: uninsneveruninstall

[InstallDelete]
; Left over from older framework-dependent builds and earlier manuals.
Type: files; Name: "{app}\Interop.Empirija.dll"
Type: files; Name: "{app}\web.config"
Type: filesandordirs; Name: "{app}\runtimes"
Type: files; Name: "{app}\empifisJSON_*.docx"
; Old framework-dependent ReceiptTester files next to the new single-file exe.
Type: files; Name: "{#TesterFolder}\ReceiptTester.dll"; Components: tester
Type: files; Name: "{#TesterFolder}\ReceiptTester.deps.json"; Components: tester
Type: files; Name: "{#TesterFolder}\ReceiptTester.runtimeconfig.json"; Components: tester
Type: files; Name: "{#TesterFolder}\ReceiptTester.pdb"; Components: tester

[Files]
Source: "{#ServiceDir}\*"; DestDir: "{app}"; Excludes: "appsettings.Development.json"; Flags: ignoreversion recursesubdirs createallsubdirs; Components: service
; Defaults as on the reference PC; an existing config.json is kept.
Source: "config.default.json"; DestDir: "{app}"; DestName: "config.json"; Flags: onlyifdoesntexist uninsneveruninstall; Components: service
Source: "{#TesterExe}"; DestDir: "{#TesterFolder}"; Flags: ignoreversion; Components: tester

[Icons]
Name: "{group}\ReceiptTester"; Filename: "{#TesterFolder}\ReceiptTester.exe"; Components: tester
Name: "{group}\EmpiFis JSON manual"; Filename: "{app}\{#ManualFile}"
Name: "{group}\Logs"; Filename: "{#LogFolder}"

[Run]
Filename: "{sys}\sc.exe"; Parameters: "start {#ServiceName}"; Description: "Start the EmpiFis JSON service now"; Flags: postinstall runhidden unchecked; Check: OfferServiceStart

[Code]
var
  ServiceExistedBefore: Boolean;
  ServiceWasAutoStart: Boolean;
  ServiceWasRunning: Boolean;

function RunHidden(const FileName, Params: String): Integer;
begin
  if not Exec(FileName, Params, '', SW_HIDE, ewWaitUntilTerminated, Result) then
    Result := -1;
  Log(Format('%s %s -> %d', [ExtractFileName(FileName), Params, Result]));
end;

function Sc(const Params: String): Integer;
begin
  Result := RunHidden(ExpandConstant('{sys}\sc.exe'), Params);
end;

{ sc query prints the state names (RUNNING, STOPPED) in English on every Windows language. }
function ServiceQueryContains(const Name, Text: String): Boolean;
begin
  Result := RunHidden(ExpandConstant('{cmd}'), '/C sc query ' + Name + ' | find "' + Text + '" >nul') = 0;
end;

function ServiceExists(const Name: String): Boolean;
begin
  Result := RunHidden(ExpandConstant('{cmd}'), '/C sc query ' + Name + ' >nul') = 0;
end;

function OfferServiceStart: Boolean;
begin
  { A service that was running before the update is started again automatically. }
  Result := not ServiceWasRunning;
end;

function InitializeSetup: Boolean;
begin
  ServiceExistedBefore := ServiceExists('{#ServiceName}');
  ServiceWasAutoStart := ServiceExistedBefore and
    (RunHidden(ExpandConstant('{cmd}'), '/C sc qc {#ServiceName} | find "AUTO_START" >nul') = 0);
  Result := True;
end;

procedure InitializeWizard;
begin
  { Show the current start type, so clicking through an update keeps it. }
  if ServiceWasAutoStart then
    WizardSelectTasks('autostart');
end;

{ /TASKS given on the command line: the caller chose the start type explicitly. }
function TasksGivenOnCommandLine: Boolean;
var
  I: Integer;
begin
  Result := False;
  for I := 1 to ParamCount do
    if Pos('/TASKS=', Uppercase(ParamStr(I))) = 1 then
      Result := True;
end;

procedure StopService;
var
  I: Integer;
begin
  if ServiceExists('{#ServiceName}') and not ServiceQueryContains('{#ServiceName}', 'STOPPED') then
  begin
    Sc('stop {#ServiceName}');
    I := 0;
    while (I < 60) and not ServiceQueryContains('{#ServiceName}', 'STOPPED') do
    begin
      Sleep(500);
      I := I + 1;
    end;
  end;
  { A service that didn't stop, or the app running in the tray, would lock the files. }
  RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#ServiceExe}');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  ServiceWasRunning := ServiceExists('{#ServiceName}') and ServiceQueryContains('{#ServiceName}', 'RUNNING');
  StopService;
  if WizardIsComponentSelected('tester') then
    RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /IM ReceiptTester.exe');
  Result := '';
end;

function ConfigText: String;
var
  Text: AnsiString;
begin
  if LoadStringFromFile(ExpandConstant('{app}\config.json'), Text) then
    Result := String(Text)
  else
    Result := '';
end;

{ Minimal reader for "Key": "value" or "Key": 123 in config.json (the paths there contain no quotes). }
function JsonValue(const Json, Key, Default: String): String;
var
  P: Integer;
  S: String;
begin
  Result := Default;
  P := Pos('"' + Key + '"', Json);
  if P = 0 then
    Exit;
  S := Copy(Json, P + Length(Key) + 2, MaxInt);
  P := Pos(':', S);
  if P = 0 then
    Exit;
  S := Trim(Copy(S, P + 1, MaxInt));
  if (S <> '') and (S[1] = '"') then
  begin
    S := Copy(S, 2, MaxInt);
    P := Pos('"', S);
    if P = 0 then
      Exit;
    S := Copy(S, 1, P - 1);
    StringChangeEx(S, '\\', '\', True);
  end
  else
  begin
    P := 1;
    while (P <= Length(S)) and (S[P] >= '0') and (S[P] <= '9') do
      P := P + 1;
    S := Copy(S, 1, P - 1);
  end;
  if S <> '' then
    Result := S;
end;

{ The file mode folders from config.json; the POS (a normal user) must be able to write requests there. }
procedure CreateFileModeFolders;
var
  Json, Folder: String;
  I: Integer;
begin
  Json := ConfigText;
  for I := 0 to 1 do
  begin
    if I = 0 then
      Folder := JsonValue(Json, 'InFilePath', 'C:\Altera\json\in\')
    else
      Folder := JsonValue(Json, 'OutFilePath', 'C:\Altera\json\out\');
    Folder := RemoveBackslashUnlessRoot(Folder);
    if ForceDirectories(Folder) then
      { *S-1-5-32-545 = the local Users group, whatever the Windows language. }
      RunHidden(ExpandConstant('{sys}\icacls.exe'), '"' + Folder + '" /grant *S-1-5-32-545:(OI)(CI)M')
    else
      Log('Could not create ' + Folder);
  end;
end;

procedure RegisterService;
var
  BinPath, StartType: String;
begin
  BinPath := ExpandConstant('{app}\{#ServiceExe}');
  if WizardIsTaskSelected('autostart') then
    StartType := 'auto'
  else
    StartType := 'demand';
  if ServiceExists('{#ServiceName}') then
  begin
    { A silent update without /TASKS keeps the existing start type (a till's automatic start must
      stay); interactively the checkbox was preset to it, so the user's choice applies. }
    if WizardSilent and not TasksGivenOnCommandLine then
      Sc('config {#ServiceName} binPath= "' + BinPath + '" DisplayName= "{#ServiceDisplayName}"')
    else
      Sc('config {#ServiceName} binPath= "' + BinPath + '" start= ' + StartType + ' DisplayName= "{#ServiceDisplayName}"');
  end
  else
    Sc('create {#ServiceName} binPath= "' + BinPath + '" start= ' + StartType + ' DisplayName= "{#ServiceDisplayName}"');
  Sc('description {#ServiceName} "Receives JSON receipts (HTTP or files) and prints them on the Empirija fiscal device."');
  { If EmpiFis gets stuck the service ends itself; Windows restarts it after 5 s, 30 s, then every 5 minutes. }
  Sc('failure {#ServiceName} reset= 86400 actions= restart/5000/restart/30000/restart/300000');
  Sc('failureflag {#ServiceName} 1');
end;

procedure AddFirewallRule;
var
  Port: String;
begin
  Port := JsonValue(ConfigText, 'port', '5006');
  RunHidden(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="{#FirewallRule}"');
  RunHidden(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall add rule name="{#FirewallRule}" dir=in action=allow protocol=TCP localport=' + Port);
end;

procedure CheckPrerequisites;
var
  Dll: String;
begin
  { EmpiFisX is a 32-bit COM server; it must be registered for 32-bit programs. }
  if not RegQueryStringValue(HKLM32, 'SOFTWARE\Classes\CLSID\{#EmpiFisXClsid}\InprocServer32', '', Dll) then
    SuppressibleMsgBox('EmpiFisX (the Empirija fiscal COM component) is not registered for 32-bit programs, so the service cannot print.' + #13#10#13#10 +
      'Install EmpiFis, or register it with:' + #13#10 + '%windir%\SysWOW64\regsvr32.exe "C:\Altera\VersionX\EmpiFisX.dll"', mbError, MB_OK, IDOK)
  else if not FileExists(Dll) then
    SuppressibleMsgBox('EmpiFisX is registered as "' + Dll + '", but that file does not exist. Reinstall EmpiFis.', mbError, MB_OK, IDOK);

  { The old v1 service used the same port and folders. }
  if ServiceExists('empifisJson') then
    SuppressibleMsgBox('An older service "empifisJson" is installed. If it runs, it uses the same port and folders as this service. ' +
      'Remove it with: sc delete empifisJson', mbInformation, MB_OK, IDOK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    CreateFileModeFolders;
    RegisterService;
    if WizardIsTaskSelected('firewall') then
      AddFirewallRule;
    CheckPrerequisites;
    if ServiceWasRunning then
      Sc('start {#ServiceName}');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    StopService;
    Sc('delete {#ServiceName}');
    RunHidden(ExpandConstant('{sys}\netsh.exe'), 'advfirewall firewall delete rule name="{#FirewallRule}"');
    RunHidden(ExpandConstant('{sys}\taskkill.exe'), '/F /IM ReceiptTester.exe');
  end;
end;
