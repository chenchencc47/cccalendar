@echo off
setlocal

pushd "%~dp0\.."

dotnet restore CcCalendar.sln
if errorlevel 1 goto :failure

dotnet format CcCalendar.sln --no-restore --verify-no-changes
if errorlevel 1 goto :failure

dotnet build CcCalendar.sln --no-restore --configuration Debug
if errorlevel 1 goto :failure

dotnet test CcCalendar.sln --no-build --configuration Debug --collect "XPlat Code Coverage" --results-directory artifacts\TestResults
if errorlevel 1 goto :failure

popd
exit /b 0

:failure
popd
exit /b 1
