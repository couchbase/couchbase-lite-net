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
cd ..\Couchbase.Lite.Support.NetDesktop
echo.
echo *** NET DESKTOP ***
echo.
dotnet restore

cd ..\Couchbase.Lite.Support.UWP
echo.
echo *** UWP ***
echo.
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" /t:Restore

echo.
echo *** IOS ***
echo.
cd ..\Couchbase.Lite.Support.Apple\iOS
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" /t:Restore

echo.
echo *** ANDROID ***
echo.
cd ..\..\Couchbase.Lite.Support.Android
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" /t:Restore

echo.
echo *** COPYING NATIVE RESOURCES ***
echo.
if not exist ..\Couchbase.Lite.Support.Apple\iOS\Resources mkdir ..\Couchbase.Lite.Support.Apple\iOS\Resources
xcopy /Y ..\..\vendor\couchbase-lite-core\build_cmake\ios-fat\libLiteCore.dylib ..\Couchbase.Lite.Support.Apple\iOS\Resources

echo.
echo *** BUILDING ***
echo.

cd ..
"C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe" Couchbase.Lite.sln /p:Configuration=Packaging /p:SourceLinkCreate=true

popd
