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
} 

Copy-Item -Recurse -Force ..\vendor\couchbase-lite-core\build_cmake\ios-fat\LiteCore.framework Couchbase.Lite.Support.Apple\iOS\Native
Write-Host *** BUILDING ***
Write-Host

& $MSBuild Couchbase.Lite.sln /p:Configuration=Packaging /p:SourceLinkCreate=true

Pop-Location
Pop-Location
