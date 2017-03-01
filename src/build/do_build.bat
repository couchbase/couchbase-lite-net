@echo off

echo This script is meant for the Couchbase build server.  It cannot be used by developers.
pushd %~dp0..\Couchbase.Lite
if not exist ..\couchbase.snk (
    echo Private key not found, aborting...
    popd
    exit /b 1
)

"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" Couchbase.Lite.csproj /t:Transform /p:TransformFile="Properties\DynamicAssemblyInfo.tt"
dotnet restore
dotnet build -c Packaging && "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.6.1 Tools\sn.exe" -Ra bin\Packaging\netstandard1.4\Couchbase.Lite.dll ..\couchbase.snk
popd