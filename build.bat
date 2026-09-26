@echo off
setlocal
rem Kompiluje AutoClickerMC.exe wbudowanym w Windows kompilatorem C# (.NET Framework 4.x).
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Nie znaleziono csc.exe - brak .NET Framework 4.x
    pause
    exit /b 1
)

"%CSC%" /nologo /codepage:65001 /target:winexe /optimize+ /platform:anycpu ^
    /out:"%~dp0AutoClickerMC.exe" ^
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    "%~dp0src\*.cs"

if errorlevel 1 (
    echo.
    echo BLAD KOMPILACJI
    pause
    exit /b 1
)
echo Gotowe: %~dp0AutoClickerMC.exe
