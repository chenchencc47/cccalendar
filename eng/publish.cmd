@echo off
setlocal

pushd "%~dp0\.."

call eng\verify.cmd
if errorlevel 1 goto :failure

dotnet publish src\CcCalendar.Desktop\CcCalendar.Desktop.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts\publish\win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 goto :failure

powershell.exe -NoProfile -ExecutionPolicy Bypass -File eng\smoke-launch.ps1 -ExecutablePath artifacts\publish\win-x64\cccalendar.exe
if errorlevel 1 goto :failure

set "CCCALENDAR_ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%CCCALENDAR_ISCC%" set "CCCALENDAR_ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%CCCALENDAR_ISCC%" set "CCCALENDAR_ISCC=%LocalAppData%\Programs\Inno Setup 6\ISCC.exe"

if not exist "%CCCALENDAR_ISCC%" (
    echo Inno Setup 6 was not found. Publish output is valid, but the installer was not built.
    goto :failure
)

"%CCCALENDAR_ISCC%" installer\cccalendar.iss
if errorlevel 1 goto :failure

popd
exit /b 0

:failure
popd
exit /b 1
