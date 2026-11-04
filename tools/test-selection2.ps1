$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W2 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@
[void][W2]::SetProcessDPIAware()

$p = Get-Process pi-client -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'NO-PROCESS'; exit 1 }
$h = $p.MainWindowHandle
[void][W2]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 500
$r = New-Object W2+RECT
[void][W2]::GetWindowRect($h, [ref]$r)
Write-Output ("rect=" + $r.L + "," + $r.T + " " + ($r.R - $r.L) + "x" + ($r.B - $r.T))

# 1) 先点一下文字区域，让 MdView 拿到焦点
$cx = $r.L + 600
$cy = $r.T + 251
[void][W2]::SetCursorPos($cx, $cy)
Start-Sleep -Milliseconds 200
[W2]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 100
[W2]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 300
Write-Output 'clicked text'

# 2) Ctrl+A 全选
[System.Windows.Forms.SendKeys]::SendWait('^a')
Start-Sleep -Milliseconds 400
[System.Windows.Forms.Clipboard]::Clear()
[System.Windows.Forms.SendKeys]::SendWait('^c')
Start-Sleep -Milliseconds 500
$clip1 = [System.Windows.Forms.Clipboard]::GetText()
Write-Output ("after ctrl+a ctrl+c => [" + $clip1 + "]")

# 3) 鼠标拖选
[void][W2]::SetCursorPos($r.L + 500, $cy)
Start-Sleep -Milliseconds 150
[W2]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 100
for ($x = $r.L + 500; $x -le ($r.L + 900); $x += 25) {
  [void][W2]::SetCursorPos($x, $cy)
  Start-Sleep -Milliseconds 20
}
Start-Sleep -Milliseconds 100
[W2]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 300
[System.Windows.Forms.Clipboard]::Clear()
[System.Windows.Forms.SendKeys]::SendWait('^c')
Start-Sleep -Milliseconds 500
$clip2 = [System.Windows.Forms.Clipboard]::GetText()
Write-Output ("after drag => [" + $clip2 + "]")

$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$crop = New-Object System.Drawing.Bitmap 1100, 300
$g2 = [System.Drawing.Graphics]::FromImage($crop)
$g2.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, 1100, 300), (New-Object System.Drawing.Rectangle ($r.L + 300), ($r.T + 120), 1100, 300), [System.Drawing.GraphicsUnit]::Pixel)
$crop.Save('C:\Users\Administrator\AppData\Local\Temp\sel-shot.png', [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output 'shot saved'
