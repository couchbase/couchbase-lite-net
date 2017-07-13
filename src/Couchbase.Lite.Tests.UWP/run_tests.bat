@echo off

msbuild /p:Configuration=Release /p:Platform=x64 Couchbase.Lite.Tests.UWP.csproj
vstest.console.exe /InIsolation /Platform:x64 AppPackages\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Test\Couchbase.Lite.Tests.UWP_1.0.0.0_x64.appx /Logger:trx