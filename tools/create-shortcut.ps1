# 把编译产物安装到固定目录，并创建桌面/开始菜单快捷方式
$ErrorActionPreference = 'Stop'

$src = Join-Path (Split-Path -Parent $PSScriptRoot) 'dist\pi-client.exe'
$readme = Join-Path (Split-Path -Parent $PSScriptRoot) 'README.txt'
$dest = Join-Path $env:LOCALAPPDATA 'Programs\pi-client'

if (-not (Test-Path $src)) { throw "找不到 $src，请先运行 build.cmd" }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item $src (Join-Path $dest 'pi-client.exe') -Force
if (Test-Path $readme) { Copy-Item $readme (Join-Path $dest 'README.txt') -Force }
Write-Host "已安装到: $dest"

$exe = Join-Path $dest 'pi-client.exe'
$shell = New-Object -ComObject WScript.Shell
$targets = @(
  (Join-Path ([Environment]::GetFolderPath('Desktop')) 'pi 客户端.lnk'),
  (Join-Path ([Environment]::GetFolderPath('Programs')) 'pi 客户端.lnk')
)
foreach ($lnkPath in $targets) {
  $lnk = $shell.CreateShortcut($lnkPath)
  $lnk.TargetPath = $exe
  $lnk.WorkingDirectory = $dest
  $lnk.IconLocation = "$exe,0"
  $lnk.Description = 'pi 客户端 —— 图形界面（调用本机已安装的 pi）'
  $lnk.WindowStyle = 1
  $lnk.Save()
  Write-Host "已创建快捷方式: $lnkPath"
}
