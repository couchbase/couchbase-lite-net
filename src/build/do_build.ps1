Push-Location $PSScriptRoot\..\Couchbase.Lite

$VSInstall = (Get-CimInstance MSFT_VSInstance).InstallLocation
if(-Not $VSInstall) {
    throw "Unable to locate VS2019 installation"
}

$MSBuild = "$VSInstall\MSBuild\Current\Bin\MSBuild.exe"

Write-Host
Write-Host *** TRANSFORMING TEMPLATES ***
Write-Host
& $MSBuild Couchbase.Lite.csproj /t:TransformAll

Write-Host
Write-Host *** RESTORING PACKAGES ***
Write-Host
dotnet restore
Push-Location ..
& $MSBuild /t:Restore Couchbase.Lite.sln

Write-Host
Write-Host *** COPYING NATIVE RESOURCES ***
Write-Host
if(-Not (Test-Path "Couchbase.Lite.Support.Apple\iOS\Native")) {
    New-Item -ItemType Directory Couchbase.Lite.Support.Apple\iOS\Native
    New-Item -ItemType Directory Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework
} 


Copy-Item -Force ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\Info.plist Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework
Copy-Item -Recurse -Force ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\ios-arm64\ Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework
Copy-Item -Recurse -Force ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\ios-arm64_x86_64-simulator\ Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework

# Windows absolutely loses its mind at any notion of symbolic links, so restructure to get rid of them
$catalystBaseDir = "..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\ios-arm64_x86_64-maccatalyst\LiteCore.framework"
$catalystDestDir = "Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework\ios-arm64_x86_64-maccatalyst\LiteCore.framework"
New-Item -ItemType Directory Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework\ios-arm64_x86_64-maccatalyst
New-Item -ItemType Directory $catalystDestDir
Copy-Item -Recurse -Force $catalystBaseDir\Versions\A\* $catalystDestDir

if(Test-Path ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\ios-arm64_x86_64-maccatalyst\dSYMs) {
    Copy-Item -Recurse -Force ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\ios-arm64_x86_64-maccatalyst\dSYMs $catalystDestDir\..
}

Write-Host *** BUILDING ***
Write-Host

& $MSBuild Couchbase.Lite.sln /p:Configuration=Packaging /p:SourceLinkCreate=true

Pop-Location
Pop-Location
