; QuickTranslate AI kurulum betiği (Inno Setup 6)
; Üretmek için: .\build.ps1 -Installer
; Inno Setup kurulumu:  winget install JRSoftware.InnoSetup

#define AppName       "QuickTranslate AI"
#define AppVersion    "1.0.0"
#define AppPublisher  "QuickTranslate AI"
#define AppExe        "QuickTranslateAI.exe"
#define AppId         "{{8C4B2E71-6A3D-4F18-9B52-0D7A1E5C9F43}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}

; Yönetici hakkı gerekmez: uygulama kullanıcı profiline kurulur,
; böylece kurulum sırasında UAC istemi çıkmaz.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=no

OutputDir=Output
OutputBaseFilename=QuickTranslateAI-Setup
SetupIconFile=..\src\QuickTranslateAI\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Windows 10 1809 ve üzeri. Cam efektleri Windows 11'de etkin olur.
MinVersion=10.0.17763

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; \
    Description: "Windows açılışında otomatik başlat"; \
    GroupDescription: "Ek seçenekler:"
Name: "desktopicon"; \
    Description: "Masaüstü kısayolu oluştur"; \
    GroupDescription: "Ek seçenekler:"; \
    Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Otomatik başlatma. Uygulama içindeki ayar da aynı anahtarı yönetir.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "QuickTranslateAI"; \
    ValueData: """{app}\{#AppExe}"" --silent"; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; \
    Description: "{#AppName} uygulamasını şimdi başlat"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kaldırmadan önce çalışan örneği kapat, aksi halde dosyalar kilitli kalır.
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden; RunOnceId: "StopApp"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\QuickTranslateAI"

[Code]
// Kaldırma sırasında Credential Manager'daki API anahtarını da temizlemeyi öner.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ResultCode: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if MsgBox('Kayıtlı API anahtarları Windows Kimlik Bilgisi Yöneticisi''nden de silinsin mi?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      Exec(ExpandConstant('{sys}\cmd.exe'),
           '/c cmdkey /delete:QuickTranslateAI:Anthropic & cmdkey /delete:QuickTranslateAI:OpenAI & cmdkey /delete:QuickTranslateAI:Custom',
           '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
end;
