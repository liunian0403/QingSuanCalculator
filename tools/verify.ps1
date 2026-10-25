# 安装后验证：用 UI Automation 驱动窗口、点击按钮、读显示屏，并截图
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$ShotDir = '.'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W32 {
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[W32]::SetProcessDPIAware() | Out-Null

$AE = [System.Windows.Automation.AutomationElement]
$CT = [System.Windows.Automation.ControlType]
$script:failures = 0
$script:checks = 0

function Get-AppWindow([int]$procId) {
    $cond = New-Object System.Windows.Automation.AndCondition(
        (New-Object System.Windows.Automation.PropertyCondition($AE::ProcessIdProperty, $procId)),
        (New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Window)))
    for ($i = 0; $i -lt 40; $i++) {
        $w = $AE::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)
        if ($w -ne $null) { return $w }
        Start-Sleep -Milliseconds 250
    }
    throw '未找到应用窗口'
}

function Find-Button($window, [string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    foreach ($el in $all) {
        if ($el.Current.ClassName -like '*BUTTON*') { return $el }
    }
    throw "找不到按钮: $name"
}

function Click($window, [string]$name) {
    $el = Find-Button $window $name
    $invoked = $false
    try {
        $p = $el.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $p.Invoke()
        $invoked = $true
    }
    catch {
        $invoked = $false
    }
    if (-not $invoked) {
        $h = [IntPtr]$el.Current.NativeWindowHandle
        if ($h -eq [IntPtr]::Zero) { throw "按钮无窗口句柄: $name" }
        [W32]::PostMessage($h, 0x00F5, [IntPtr]0, [IntPtr]0) | Out-Null   # BM_CLICK
    }
    Start-Sleep -Milliseconds 150
}

function Get-Display($window) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::ControlTypeProperty, $CT::Text)
    $all = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $best = $null
    $bestTop = -1.0
    foreach ($t in $all) {
        if ($t.Current.ClassName -notlike '*STATIC*') { continue }
        $n = $t.Current.Name
        if ([string]::IsNullOrEmpty($n)) { continue }
        $r = $t.Current.BoundingRectangle
        if ($r.Height -gt 0 -and $r.Top -gt $bestTop) { $bestTop = $r.Top; $best = $t }
    }
    if ($best -eq $null) { return '' }
    return $best.Current.Name
}

function Send-Key([IntPtr]$hwnd, [int]$vk) {
    [W32]::PostMessage($hwnd, 0x0100, [IntPtr]$vk, [IntPtr]0) | Out-Null
    Start-Sleep -Milliseconds 40
    [W32]::PostMessage($hwnd, 0x0101, [IntPtr]$vk, [IntPtr]0) | Out-Null
    Start-Sleep -Milliseconds 60
}

function Check([string]$label, [string]$expected, $window) {
    $actual = Get-Display $window
    $script:checks++
    if ($actual -eq $expected) {
        Write-Output ("  [PASS] {0}: {1}" -f $label, $actual)
    }
    else {
        $script:failures++
        Write-Output ("  [FAIL] {0}: 期望 '{1}' 实际 '{2}'" -f $label, $expected, $actual)
    }
}

function Capture([IntPtr]$hwnd, [string]$path) {
    $rect = New-Object W32+RECT
    [W32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    [W32]::PrintWindow($hwnd, $hdc, 2) | Out-Null
    $g.ReleaseHdc($hdc)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Output ("  [SHOT] {0} ({1}x{2})" -f $path, $w, $h)
}

function Clear-All($window) {
    Click $window 'C'
}

Write-Output "== 启动 $Exe =="
$proc = Start-Process -FilePath $Exe -PassThru
$window = Get-AppWindow $proc.Id
$hwnd = [IntPtr]$window.Current.NativeWindowHandle
Start-Sleep -Milliseconds 600
Write-Output "  主窗口: $($window.Current.Name)  pid=$($proc.Id)"

Write-Output "== 标准型功能 =="
Clear-All $window
Click $window '7'; Click $window '×'; Click $window '8'; Click $window '='
Check '7×8=' '56' $window

Clear-All $window
Click $window '2'; Click $window '0'; Click $window '0'; Click $window '+'
Click $window '1'; Click $window '0'; Click $window '%'
Check '200+10% (中间值)' '20' $window
Click $window '='
Check '200+10%=' '220' $window

Clear-All $window
Click $window '1'; Click $window '2'; Click $window '3'; Click $window '4'
Check '千分位' '1,234' $window
Click $window '←'
Check '退格' '123' $window

Clear-All $window
Click $window '5'; Click $window '±'
Check '正负号' '-5' $window

Clear-All $window
Click $window '4'; Click $window '1/x'
Check '1/4' '0.25' $window

Clear-All $window
Click $window '9'; Click $window '√x'
Check '√9' '3' $window

Clear-All $window
Click $window '6'; Click $window 'CE'
Check 'CE' '0' $window

Clear-All $window
Click $window '1'; Click $window '0'; Click $window '÷'; Click $window '0'; Click $window '='
Check '除以零提示' '除数不能为零' $window

Write-Output "== 键盘输入 =="
Clear-All $window
Send-Key $hwnd 0x34   # 4
Send-Key $hwnd 0x35   # 5
Send-Key $hwnd 0x6A   # 小键盘 *
Send-Key $hwnd 0x32   # 2
Send-Key $hwnd 0x0D   # Enter
Check '键盘 45*2 Enter' '90' $window

Clear-All $window
Send-Key $hwnd 0x38   # 8
Send-Key $hwnd 0x08   # Backspace
Check '键盘 Backspace' '0' $window

Write-Output "== 科学型功能 =="
Click $window '科学'
Start-Sleep -Milliseconds 700
$window = Get-AppWindow $proc.Id
$hwnd = [IntPtr]$window.Current.NativeWindowHandle
Capture $hwnd (Join-Path $ShotDir 'sci.png')

Clear-All $window
Click $window '3'; Click $window '0'; Click $window 'sin'
Check 'sin30(DEG)' '0.5' $window

Clear-All $window
Click $window '2'; Click $window 'x^y'; Click $window '1'; Click $window '0'; Click $window '='
Check '2^10' '1,024' $window

Clear-All $window
Click $window '1'; Click $window '0'; Click $window 'mod'; Click $window '3'; Click $window '='
Check '10 mod 3' '1' $window

Clear-All $window
Click $window '5'; Click $window 'n!'
Check '5!' '120' $window

Clear-All $window
Click $window '2'; Click $window 'x³'
Check '2³' '8' $window

Clear-All $window
Click $window '8'; Click $window '³√x'
Check '³√8' '2' $window

Clear-All $window
Click $window '1'; Click $window '0'; Click $window '0'; Click $window 'log'
Check 'log100' '2' $window

Clear-All $window
Click $window 'π'
Check 'π' '3.14159265359' $window

Write-Output "== 回到标准型并截图 =="
Click $window '标准'
Start-Sleep -Milliseconds 700
$window = Get-AppWindow $proc.Id
$hwnd = [IntPtr]$window.Current.NativeWindowHandle
Clear-All $window
Click $window '1'; Click $window '2'; Click $window '3'; Click $window '×'; Click $window '4'; Click $window '5'; Click $window '='
Capture $hwnd (Join-Path $ShotDir 'std.png')

Write-Output "== 关闭 =="
$proc.CloseMainWindow() | Out-Null
Start-Sleep -Milliseconds 800
if (-not $proc.HasExited) { $proc.Kill() }

Write-Output ''
Write-Output ("结果: {0} 项通过, {1} 项失败" -f ($script:checks - $script:failures), $script:failures)
if ($script:failures -gt 0) { exit 1 }
