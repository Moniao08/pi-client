$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W3 {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@
[void][W3]::SetProcessDPIAware()

function Shot($path, $rect) {
  $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
  $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
  $crop = New-Object System.Drawing.Bitmap $rect.Width, $rect.Height
  $g2 = [System.Drawing.Graphics]::FromImage($crop)
  $g2.DrawImage($bmp, (New-Object System.Drawing.Rectangle 0, 0, $rect.Width, $rect.Height), $rect, [System.Drawing.GraphicsUnit]::Pixel)
  $crop.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

$p = Get-Process pi-client -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) { Write-Output 'NO-PROCESS'; exit 1 }
$h = $p.MainWindowHandle
[void][W3]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 500
$r = New-Object W3+RECT
[void][W3]::GetWindowRect($h, [ref]$r)
Write-Output ("rect=" + $r.L + "," + $r.T + " " + ($r.R - $r.L) + "x" + ($r.B - $r.T))

# 先点一下对话区，让输入框失去焦点
[void][W3]::SetCursorPos(($r.L + 700), ($r.T + 560))
Start-Sleep -Milliseconds 150
[W3]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
[W3]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 500

$comp = New-Object System.Drawing.Rectangle ($r.L + 300), ($r.B - 130), (($r.R - $r.L) - 320), 120
Shot 'C:\Users\Administrator\AppData\Local\Temp\ph-blur.png' $comp

# 点输入框
[void][W3]::SetCursorPos(($r.L + 500), ($r.B - 106))
Start-Sleep -Milliseconds 150
[W3]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 60
[W3]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
Start-Sleep -Milliseconds 400
Shot 'C:\Users\Administrator\AppData\Local\Temp\ph-focus.png' $comp
Write-Output 'shots ok'
