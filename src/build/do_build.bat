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

"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" Couchbase.Lite.csproj /t:Transform /p:TransformFile="Properties\DynamicAssemblyInfo.tt"
dotnet restore -s %NUGET_REPO% -s https://api.nuget.org/v3/index.json
pushd ..\Couchbase.Lite.Support.NetDesktop
dotnet restore -s %NUGET_REPO% -s https://api.nuget.org/v3/index.json
popd

pushd ..\Couchbase.Lite.Support.UWP
if not exist ..\..\nuget.exe (
    powershell -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile ..\..\nuget.exe"
)
..\..\nuget.exe restore -Source "%NUGET_REPO%;https://api.nuget.org/v3/index.json"
popd

mkdir ..\Couchbase.Lite.Support.Apple\iOS\Resources
mkdir ..\Couchbase.Lite.Support.Apple\tvOS\Resources
xcopy ..\..\vendor\couchbase-lite-core\build_cmake\ios-fat\libLiteCore.dylib ..\Couchbase.Lite.Support.Apple\iOS\Resources
xcopy ..\..\vendor\couchbase-lite-core\build_cmake\tvos-fat\libLiteCore.dylib ..\Couchbase.Lite.Support.Apple\tvOS\Resources

pushd ..
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" Couchbase.Lite.sln /p:Configuration=Packaging
popd

popd

