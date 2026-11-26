@echo off
rem ============================================================
rem  NetHelp - сборка из исходников на любом компьютере Windows.
rem  Используется компилятор C# из состава .NET Framework, который
rem  уже есть в Windows 10/11. Устанавливать ничего не нужно.
rem ============================================================
setlocal
cd /d "%~dp0"

set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FW%\csc.exe" set "FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FW%\csc.exe"

if not exist "%CSC%" (
  echo [ОШИБКА] Не найден компилятор C#: "%CSC%"
  echo Включите компонент ".NET Framework 4.8" в компонентах Windows.
  exit /b 1
)

echo Компилятор : %CSC%
echo Исходники  : %CD%\src\*.cs
echo.

"%CSC%" /nologo /target:winexe /out:NetHelp.exe /win32manifest:app.manifest /win32icon:NetHelp.ico /resource:NetHelp.ico,NetHelp.NetHelp.ico /optimize+ /platform:anycpu /codepage:65001 /utf8output /r:"%FW%\System.dll" /r:"%FW%\System.Core.dll" /r:"%FW%\System.Drawing.dll" /r:"%FW%\System.Windows.Forms.dll" /r:"%FW%\System.Management.dll" /r:"%FW%\System.Xml.dll" src\*.cs

if errorlevel 1 (
  echo.
  echo [ОШИБКА] Сборка не удалась - смотрите сообщения компилятора выше.
  exit /b 1
)

echo.
echo Готово: %CD%\NetHelp.exe
echo Windows 7/8/10/11. Права администратора программа запрашивает сама.
endlocal
