@echo off

echo This script is meant for the Couchbase build server.  It cannot be used by developers.
pushd %~dp0..\Couchbase.Lite
if not exist ..\couchbase.snk (
    echo Private key not found, aborting...
    popd
    exit /b 1
)
if not defined VSCMD_ARG_HOST_ARCH (
    call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Common7\Tools\VsDevCmd.bat"
)

msbuild Couchbase.Lite.csproj /t:Transform /p:TransformFile="Properties\DynamicAssemblyInfo.tt"
dotnet restore
dotnet build -c Packaging && sn -Ra bin\Packaging\netstandard1.4\Couchbase.Lite.dll ..\couchbase.snk
popd