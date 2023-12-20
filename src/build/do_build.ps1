Push-Location $PSScriptRoot\..\Couchbase.Lite
if(-Not $env:NUGET_VERSION) {
    Pop-Location
    throw "NUGET_VERSION not defined, aborting..."
}

$VSInstall = (Get-CimInstance MSFT_VSInstance).InstallLocation
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
