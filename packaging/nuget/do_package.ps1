[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bOR [Net.SecurityProtocolType]::Tls12
Push-Location $PSScriptRoot\..\..\src\packages
Remove-Item *.nupkg
Remove-Item *.snupkg
$VSInstall = (Get-CimInstance MSFT_VSInstance).InstallLocation
if(-Not $VSInstall) {
    Pop-Location
    throw "Unable to locate VS installation"
}

$MSBuild = "$VSInstall\MSBuild\Current\Bin\MSBuild.exe"

if(-Not $env:NUGET_VERSION) {
    Pop-Location
    throw "NUGET_VERSION not defined, aborting..."
}

if(-Not $env:API_KEY) {
    Pop-Location
    throw "API_KEY not defined, aborting..."
}

if(-Not $env:NUGET_REPO) {
    Pop-Location
    throw "NUGET_REPO not defined, aborting..."
}

if($env:WORKSPACE) {
    Copy-Item "$env:WORKSPACE\product-texts\mobile\couchbase-lite\license\LICENSE_community.txt" "$PSScriptRoot\LICENSE.txt"
}

Write-Host
Write-Host *** RESTORING DEP PACKAGES ***
Write-Host

Push-Location ..
& $MSBuild /t:Restore Couchbase.Lite.sln

Write-Host *** PACKING ***
Write-Host

& $MSBuild Couchbase.Lite.sln /t:Pack /p:Configuration=Packaging /p:Version=$env:NUGET_VERSION

# Workaround the inability to pin a version of a ProjectReference in csproj
Push-Location $PSScriptRoot\..\..\src\packages
Remove-Item -Force -Recurse tmp -ErrorAction Ignore
New-Item -ItemType Directory tmp
[System.IO.Compression.ZipFile]::ExtractToDirectory("$pwd\Couchbase.Lite.$env:NUGET_VERSION.nupkg", "$pwd\tmp")
Push-Location tmp
$nuspecContent = $(Get-Content -Path "Couchbase.Lite.nuspec").Replace("version=`"$env:NUGET_VERSION`"", "version=`"[${env:NUGET_VERSION}]`"")
Set-Content -Path "Couchbase.Lite.nuspec" $nuspecContent
Pop-Location
Remove-Item -Path "Couchbase.Lite.$env:NUGET_VERSION.nupkg" -ErrorAction Ignore -Force
& 7z a -tzip "Couchbase.Lite.$env:NUGET_VERSION.nupkg" ".\tmp\*"
Remove-Item -Force -Recurse tmp
Pop-Location
# End Workaround

Get-ChildItem "." -Filter *.nupkg |
ForEach-Object {
    dotnet nuget push $_.Name --disable-buffering  --api-key $env:API_KEY --source $env:NUGET_REPO
    if($LASTEXITCODE) {
        Pop-Location
        throw "Failed to push $_"
    }
}

Pop-Location
Pop-Location

Pop-Location