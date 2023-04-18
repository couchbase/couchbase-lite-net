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

Write-Host *** BUILDING ***
Write-Host

& $MSBuild Couchbase.Lite.sln /p:Configuration=Packaging /p:SourceLinkCreate=true

Pop-Location
Pop-Location
