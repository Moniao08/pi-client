@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

rem ============================================================
rem  Build with the C# compiler that ships with Windows
rem  (no Visual Studio / .NET SDK required).
rem  Output: dist\pi-client.exe - single file, ~200 KB, no deps.
rem ============================================================

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [x] csc.exe not found ^(should come with .NET Framework 4.x^)
  exit /b 1
)

if not exist "dist" mkdir "dist"

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /out:"dist\pi-client.exe" /win32icon:"icon.ico" /win32manifest:"app.manifest" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll src\*.cs
if errorlevel 1 (
  echo.
  echo [x] build failed
  exit /b 1
)

echo.
echo [ok] dist\pi-client.exe
for %%F in ("dist\pi-client.exe") do echo      size: %%~zF bytes
