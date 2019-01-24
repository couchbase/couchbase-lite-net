@echo off

powershell Remove-AppxPackage -Package "eb9a8775-4322-4e36-a34c-eee261241f4e_1.0.0.0_x64__1v3rwxh47wwxj"

rmdir /s/q bin
rmdir /s/q obj
rmdir /s/q AppPackages

msbuild /t:Restore Couchbase.Lite.Tests.UWP.csproj
msbuild /p:Configuration=Debug /p:Platform=x64 /t:Rebuild Couchbase.Lite.Tests.UWP.csproj || goto :error
vstest.console.exe /InIsolation /Platform:x64 AppPackages\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Debug_Test\Couchbase.Lite.Tests.UWP_1.0.0.0_x64_Debug.appx /Logger:trx /diag:diagnostic.txt || goto :error

powershell Remove-AppxPackage -Package "eb9a8775-4322-4e36-a34c-eee261241f4e_1.0.0.0_x64__1v3rwxh47wwxj"

rmdir /s/q bin
rmdir /s/q obj
rmdir /s/q AppPackages

msbuild /t:Restore /p:Configuration=Release Couchbase.Lite.Tests.UWP.csproj
msbuild /p:Configuration=Release /p:Platform=x64 /t:Rebuild Couchbase.Lite.Tests.UWP.csproj || goto :error
vstest.console.exe /InIsolation /Platform:x64 bin\x64\Release\Couchbase.Lite.Tests.UWP.build.appxrecipe /Logger:trx /diag:diagnostic.txt /Framework:FrameworkUap10 || goto :error
goto :EOF

:error
echo Failed with error #%errorlevel%.
exit /b %errorlevel%