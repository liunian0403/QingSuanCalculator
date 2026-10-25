# 构建脚本：图标 -> 编译 exe -> 打包 Inno Setup 安装包
# 用法： powershell -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
$build = Join-Path $root 'build'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) { throw '.NET Framework C# 编译器 (csc.exe) 未找到' }

$iscc = Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $iscc)) { $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' }
if (-not (Test-Path $iscc)) { throw 'Inno Setup 6 (ISCC.exe) 未找到：winget install JRSoftware.InnoSetup' }

New-Item -ItemType Directory -Path $dist -Force | Out-Null

Write-Host '[1/3] 生成图标...' -ForegroundColor Cyan
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $build 'make-icon.ps1') -Out (Join-Path $build 'app.ico')
if ($LASTEXITCODE -ne 0) { throw '图标生成失败' }

Write-Host '[2/3] 编译 Calculator.exe...' -ForegroundColor Cyan
& $csc /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    "/win32icon:$(Join-Path $build 'app.ico')" `
    "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
    "/out:$(Join-Path $dist 'Calculator.exe')" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    (Join-Path $root 'src\Calculator.cs')
if ($LASTEXITCODE -ne 0) { throw '编译失败' }

Write-Host '[3/3] 打包安装程序...' -ForegroundColor Cyan
& $iscc "/Qp" (Join-Path $build 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup 打包失败' }

Write-Host ''
Write-Host '构建完成：' -ForegroundColor Green
Get-ChildItem $dist -Filter *.exe | ForEach-Object {
    Write-Host ("  {0}  ({1:N1} KB)" -f $_.FullName, ($_.Length / 1KB))
}
