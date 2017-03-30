@echo off

msbuild /p:Configuration=Packaging;Platform=x64 Couchbase.Lite.Tests.UWP.csproj
vstest.console.exe /InIsolation /Platform:x64 AppPackages\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Packaging_Test\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Packaging.appx