$ErrorActionPreference = 'Continue'
Add-Type -AssemblyName System.Windows.Forms
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class W {
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, int dx, int dy, uint d, UIntPtr e);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
}
'@
[void][W]::SetProcessDPIAware()

$exe = 'C:\Users\Administrator\pi-client\dist\pi-client.exe'
$dir = 'C:\Users\Administrator\AppData\Local\Temp\seltest'
Remove-Item -Recurse -Force $dir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Copy-Item $exe $dir
# 预置配置：固定窗口位置，便于计算坐标
@'
{
  "piHome": "C:\\Users\\Administrator\\AppData\\Local\\pi-node\\current",
  "lastCwd": "C:\\Users\\Administrator",
  "winX": 100,
  "winY": 40,
  "winW": 1475,
  "winH": 900,
  "winMax": false,
  "recentDirs": ["C:\\Users\\Administrator"]
}
'@ | Set-Content -Encoding UTF8 (Join-Path $dir 'config.json')

$p = Start-Process -FilePath (Join-Path $dir 'pi-client.exe') -PassThru
Start-Sleep -Seconds 8
$p.Refresh()
$h = $p.MainWindowHandle
Write-Output ("window=" + $h)
[void][W]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 600

# 输入并发送
[System.Windows.Forms.SendKeys]::SendWait('只回复一句：这是用于测试文字选取的句子，请勿调用工具。')
Start-Sleep -Milliseconds 300
[System.Windows.Forms.SendKeys]::SendWait('{ENTER}')
Write-Output 'sent, waiting for reply...'
Start-Sleep -Seconds 14

$r = New-Object W+RECT
[void][W]::GetWindowRect($h, [ref]$r)
Write-Output ("rect=" + $r.L + "," + $r.T + " " + ($r.R - $r.L) + "x" + ($r.B - $r.T))

# 在对话区自上而下扫几行做拖选，直到剪贴板有内容
$clip = ''
foreach ($y in @(210, 230, 250, 270, 290, 310, 330, 350)) {
  $ay = $r.T + $y
  $x1 = $r.L + 420
  $x2 = $r.L + 1150
  [void][W]::SetCursorPos($x1, $ay)
  Start-Sleep -Milliseconds 150
  [W]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)   # left down
  Start-Sleep -Milliseconds 80
  for ($x = $x1; $x -le $x2; $x += 40) {
    [void][W]::SetCursorPos($x, $ay)
    Start-Sleep -Milliseconds 12
  }
  [void][W]::SetCursorPos($x2, $ay)
  Start-Sleep -Milliseconds 80
  [W]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)   # left up
  Start-Sleep -Milliseconds 250
  [System.Windows.Forms.Clipboard]::Clear()
  [System.Windows.Forms.SendKeys]::SendWait('^c')
  Start-Sleep -Milliseconds 400
  $clip = [System.Windows.Forms.Clipboard]::GetText()
  if ($clip -and $clip.Trim().Length -gt 0) {
    Write-Output ("SELECTED at y=" + $y + " => " + $clip)
    break
  }
}
if (-not $clip -or $clip.Trim().Length -eq 0) { Write-Output 'SELECT-FAIL: 剪贴板为空' }

Start-Sleep -Milliseconds 300
$shot = Join-Path $dir 'after.png'
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save($shot, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output ("screenshot=" + $shot)
