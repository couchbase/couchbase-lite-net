Push-Location $PSScriptRoot\..\Couchbase.Lite

$VSInstall = (Get-CimInstance MSFT_VSInstance).InstallLocation
if(-Not $VSInstall) {
    throw "Unable to locate VS installation"
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

Remove-Item -Recurse -Force "Couchbase.Lite.Support.Apple\iOS\Native\"
New-Item -ItemType Directory Couchbase.Lite.Support.Apple\iOS\Native
New-Item -ItemType Directory Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework

Copy-Item -Force ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\Info.plist Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework
Copy-Item -Recurse -Force ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\ios-arm64\ Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework
Copy-Item -Recurse -Force ..\vendor\couchbase-lite-core\build_cmake\ios\LiteCore.xcframework\ios-arm64_x86_64-simulator\ Couchbase.Lite.Support.Apple\iOS\Native\LiteCore.xcframework

# Note that the mac catalyst slice is not copied above, this is on purpose since .NET 6 will directly use a zip file, whereas Xamarin
# iOS needs an extracted framework to work with (and mac catalyst is not needed)

Write-Host *** BUILDING ***
Write-Host

& $MSBuild Couchbase.Lite.sln /p:Configuration=Packaging /p:SourceLinkCreate=true

Pop-Location
Pop-Location
