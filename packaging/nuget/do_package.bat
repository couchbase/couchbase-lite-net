@echo off
pushd %~dp0
if not exist ..\..\nuget.exe (
    powershell -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile ..\..\nuget.exe"
)

if not defined NUGET_VERSION (
    echo NUGET_VERSION not defined, aborting...
    popd
    exit /b 1
)

if not defined API_KEY (
    echo API_KEY not defined, aborting...
    popd
    exit /b 1
)

if not defined NUGET_REPO (
    echo NUGET_REPO not defined, aborting...
    popd
    exit /b 1
)

for /r %%i in (*.nuspec) do (
    ..\..\nuget.exe pack %%i /Properties version=%NUGET_VERSION% /BasePath ..\.. || exit /b 1
)

for /r %%i in (*.nupkg) do (
    ..\..\nuget.exe push %%i %API_KEY% -Source %NUGET_REPO% || exit /b 1
)

del *.nupkg

popd