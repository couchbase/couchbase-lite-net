@echo off

echo This script is meant for the Couchbase build server.  It cannot be used by developers.
pushd %~dp0..\Couchbase.Lite
if not exist ..\couchbase.snk (
    echo Private key not found, aborting...
    popd
    exit /b 1
)

if not defined NUGET_REPO (
    echo NUGET_REPO not defined, aborting...
    popd
    exit /b 1
)

echo.
echo *** TRANSFORMING TEMPLATES ***
echo.
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" Couchbase.Lite.csproj /t:Transform /p:TransformFile="Properties\DynamicAssemblyInfo.tt"

echo.
echo *** RESTORING PACKAGES ***
echo *** MAIN ASSEMBLY ***
echo.
dotnet restore -s %NUGET_REPO% -s https://api.nuget.org/v3/index.json
pushd ..\Couchbase.Lite.Support.NetDesktop
echo.
echo *** NET DESKTOP ***
echo.
dotnet restore -s %NUGET_REPO% -s https://api.nuget.org/v3/index.json
popd

pushd ..\Couchbase.Lite.Support.UWP
if not exist ..\..\nuget.exe (
    powershell -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile ..\..\nuget.exe"
)
echo.
echo *** UWP ***
echo.
..\..\nuget.exe restore -Source "%NUGET_REPO%;https://api.nuget.org/v3/index.json" Couchbase.Lite.Support.UWP.csproj
popd
echo.
echo *** IOS ***
echo.
pushd ..\Couchbase.Lite.Support.Apple\iOS
..\..\..\nuget.exe restore -Source "%NUGET_REPO%;https://api.nuget.org/v3/index.json" -SolutionDirectory ..\..
popd
echo.
echo *** ANDROID ***
echo.
pushd ..\Couchbase.Lite.Support.Android
..\..\nuget.exe restore -Source "%NUGET_REPO%;https://api.nuget.org/v3/index.json" -SolutionDirectory ..
popd

echo.
echo *** COPYING NATIVE RESOURCES ***
echo.
if not exist ..\Couchbase.Lite.Support.Apple\iOS\Resources mkdir ..\Couchbase.Lite.Support.Apple\iOS\Resources
xcopy /Y ..\..\vendor\couchbase-lite-core\build_cmake\ios-fat\libLiteCore.dylib ..\Couchbase.Lite.Support.Apple\iOS\Resources
pushd ..

echo.
echo *** BUILDING ***
echo.
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" Couchbase.Lite.sln /p:Configuration=Packaging
REM echo.
REM echo *** SIGNING ***
REM echo *** MAIN ASSEMBLY ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Ra Couchbase.Lite\bin\Packaging\netstandard1.4\Couchbase.Lite.dll couchbase.snk
REM echo.
REM echo *** ANDROID ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Ra Couchbase.Lite.Support.Android\bin\Packaging\Couchbase.Lite.Support.Android.dll couchbase.snk
REM echo.
REM echo *** IOS ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Ra Couchbase.Lite.Support.Apple\iOS\bin\Packaging\Couchbase.Lite.Support.iOS.dll couchbase.snk
REM echo.
REM echo *** NET DESKTOP ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Ra Couchbase.Lite.Support.NetDesktop\bin\Packaging\netstandard1.4\Couchbase.Lite.Support.NetDesktop.dll couchbase.snk
REM echo.
REM echo *** UWP ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Ra Couchbase.Lite.Support.UWP\bin\x64\Packaging\Couchbase.Lite.Support.UWP.dll couchbase.snk

REM echo.
REM echo *** VERIFYING SIGNATURES ***
REM echo *** MAIN ASSEMBLY ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -v Couchbase.Lite\bin\Packaging\netstandard1.4\Couchbase.Lite.dll || exit /b 1
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Tp Couchbase.Lite\bin\Packaging\netstandard1.4\Couchbase.Lite.dll
REM echo.
REM echo *** ANDROID ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -v Couchbase.Lite.Support.Android\bin\Packaging\Couchbase.Lite.Support.Android.dll || exit /b 1
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Tp Couchbase.Lite.Support.Android\bin\Packaging\Couchbase.Lite.Support.Android.dll
REM echo.
REM echo *** IOS ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -v Couchbase.Lite.Support.Apple\iOS\bin\Packaging\Couchbase.Lite.Support.iOS.dll || exit /b 1
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Tp Couchbase.Lite.Support.Apple\iOS\bin\Packaging\Couchbase.Lite.Support.iOS.dll
REM echo.
REM echo *** NET DESKTOP ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -v Couchbase.Lite.Support.NetDesktop\bin\Packaging\netstandard1.4\Couchbase.Lite.Support.NetDesktop.dll || exit /b 1
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Tp Couchbase.Lite.Support.NetDesktop\bin\Packaging\netstandard1.4\Couchbase.Lite.Support.NetDesktop.dll
REM echo.
REM echo *** UWP ***
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -v Couchbase.Lite.Support.UWP\bin\x64\Packaging\Couchbase.Lite.Support.UWP.dll || exit /b 1
REM "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Tp Couchbase.Lite.Support.UWP\bin\x64\Packaging\Couchbase.Lite.Support.UWP.dll
popd
popd
