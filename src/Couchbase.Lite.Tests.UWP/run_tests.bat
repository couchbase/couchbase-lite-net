@echo off

msbuild /p:Configuration=Release /p:Platform=x64 Couchbase.Lite.Tests.UWP.csproj
vstest.console.exe /InIsolation /Platform:x64 /Framework:FrameworkUap10 bin\x64\Release\Couchbase.Lite.Tests.UWP.build.appxrecipe /Logger:trx