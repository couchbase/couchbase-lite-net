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

..\..\nuget.exe pack couchbase-lite.nuspec /Properties version=%NUGET_VERSION% /BasePath ..\..
..\..\nuget.exe pack couchbase-lite-support-uwp.nuspec /Properties version=%NUGET_VERSION% /BasePath ..\..
..\..\nuget.exe pack couchbase-lite-support-android.nuspec /Properties version=%NUGET_VERSION% /BasePath ..\..
..\..\nuget.exe pack couchbase-lite-support-ios.nuspec /Properties version=%NUGET_VERSION% /BasePath ..\..
..\..\nuget.exe pack couchbase-lite-support-tvos.nuspec /Properties version=%NUGET_VERSION% /BasePath ..\..
..\..\nuget.exe push Couchbase.Lite.%NUGET_VERSION%.nupkg %API_KEY% -Source %NUGET_REPO%
..\..\nuget.exe push Couchbase.Lite.Support.UWP.%NUGET_VERSION%.nupkg %API_KEY% -Source %NUGET_REPO%
..\..\nuget.exe push Couchbase.Lite.Support.Android.%NUGET_VERSION%.nupkg %API_KEY% -Source %NUGET_REPO%
..\..\nuget.exe push Couchbase.Lite.Support.iOS.%NUGET_VERSION%.nupkg %API_KEY% -Source %NUGET_REPO%
..\..\nuget.exe push Couchbase.Lite.Support.tvOS.%NUGET_VERSION%.nupkg %API_KEY% -Source %NUGET_REPO%

del *.nupkg

popd