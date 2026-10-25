[Setup]
; 轻算计算器 安装包脚本（Inno Setup 6）
AppId={{7E2C4B1A-9D3F-4A55-9C2B-6F1A8E0D5B31}
AppName=轻算计算器
AppVersion=1.0.0
AppVerName=轻算计算器 1.0.0
AppPublisher=QingSuan
AppPublisherURL=
AppSupportURL=
AppUpdatesURL=
DefaultDirName={autopf}\QingSuan Calculator
DefaultGroupName=轻算计算器
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
OutputDir=..\dist
OutputBaseFilename=QingSuanCalculator-Setup-1.0.0
SetupIconFile=app.ico
UninstallDisplayIcon={app}\Calculator.exe
UninstallDisplayName=轻算计算器
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=6.1sp1
AllowNoIcons=yes
CloseApplications=yes
LanguageDetectionMethod=locale
ShowLanguageDialog=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "lang\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimplified.CreateDesktopIcon=创建桌面快捷方式
english.CreateDesktopIcon=Create a desktop shortcut

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\dist\Calculator.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "README.txt"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\轻算计算器"; Filename: "{app}\Calculator.exe"; Comment: "标准型 / 科学型计算器"
Name: "{group}\卸载 轻算计算器"; Filename: "{uninstallexe}"
Name: "{autodesktop}\轻算计算器"; Filename: "{app}\Calculator.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Calculator.exe"; Description: "{cm:LaunchProgram,轻算计算器}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
