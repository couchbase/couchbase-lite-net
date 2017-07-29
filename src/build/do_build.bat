@echo off

pushd %~dp0..\Couchbase.Lite

echo.
echo *** TRANSFORMING TEMPLATES ***
echo.
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" Couchbase.Lite.csproj /t:Transform /p:TransformFile="Properties\DynamicAssemblyInfo.tt"

echo.
echo *** RESTORING PACKAGES ***
echo *** MAIN ASSEMBLY ***
echo.
dotnet restore
pushd ..\Couchbase.Lite.Support.NetDesktop
echo.
echo *** NET DESKTOP ***
echo.
dotnet restore
popd

pushd ..\Couchbase.Lite.Support.UWP
if not exist ..\..\nuget.exe (
    powershell -Command "Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile ..\..\nuget.exe"
)
echo.
echo *** UWP ***
echo.
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" /t:Restore
popd
echo.
echo *** IOS ***
echo.
pushd ..\Couchbase.Lite.Support.Apple\iOS
..\..\..\nuget.exe restore -SolutionDirectory ..\..
popd
echo.
echo *** ANDROID ***
echo.
pushd ..\Couchbase.Lite.Support.Android
..\..\nuget.exe restore -SolutionDirectory ..
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
popd
popd
