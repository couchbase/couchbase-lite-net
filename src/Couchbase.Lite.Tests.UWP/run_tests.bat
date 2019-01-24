@echo off

powershell Remove-AppxPackage -Package "eb9a8775-4322-4e36-a34c-eee261241f4e_1.0.0.0_x64__1v3rwxh47wwxj"

rmdir /s/q bin
rmdir /s/q obj
rmdir /s/q AppPackages

msbuild /t:Restore Couchbase.Lite.Tests.UWP.csproj
msbuild /p:Configuration=Debug /p:Platform=x64 /t:Rebuild Couchbase.Lite.Tests.UWP.csproj
vstest.console.exe /InIsolation /Platform:x64 AppPackages\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Debug_Test\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Debug.appx /Logger:trx /diag:diagnostic.txt

powershell Remove-AppxPackage -Package "eb9a8775-4322-4e36-a34c-eee261241f4e_1.0.0.0_x64__1v3rwxh47wwxj"

rmdir /s/q bin
rmdir /s/q obj
rmdir /s/q AppPackages

msbuild /t:Restore /p:Configuration=Release Couchbase.Lite.Tests.UWP.csproj
msbuild /p:Configuration=Release /p:Platform=x64 /t:Rebuild Couchbase.Lite.Tests.UWP.csproj
vstest.console.exe /InIsolation /Platform:x64 AppPackages\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Test\Couchbase.Lite.Tests.UWP_1.0.0.0_x64.appx /Logger:trx /diag:diagnostic.txt