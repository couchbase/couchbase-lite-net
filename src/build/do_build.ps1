Push-Location $PSScriptRoot\..\Couchbase.Lite
if(-Not $env:NUGET_VERSION) {
    Pop-Location
    throw "NUGET_VERSION not defined, aborting..."
}

$VSInstall = (& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -requires Microsoft.NetCore.Component.SDK -requires Microsoft.NetCore.Component.Runtime.8.0 -property resolvedInstallationPath)
if(-Not $VSInstall) {
    Pop-Location
    throw "Unable to locate VS installation"
}

$MSBuild = "$VSInstall\MSBuild\Current\Bin\MSBuild.exe"

Write-Host
Write-Host *** RESTORING PACKAGES ***
Write-Host
dotnet restore
Push-Location ..
& $MSBuild /t:Restore Couchbase.Lite.sln

Write-Host *** BUILDING ***
Write-Host

& $MSBuild Couchbase.Lite.sln /p:Configuration=Packaging /p:Version=$env:NUGET_VERSION

Pop-Location
Pop-Location
