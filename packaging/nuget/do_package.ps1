[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bOR [Net.SecurityProtocolType]::Tls12

Remove-Item $PSScriptRoot\..\..\src\packages\*.nupkg -ErrorAction Ignore
Remove-Item $PSScriptRoot\..\..\src\packages\*.snupkg -ErrorAction Ignore

$VSInstall = (& 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe' -latest -requires Microsoft.NetCore.Component.SDK -requires Microsoft.NetCore.Component.Runtime.8.0 -property resolvedInstallationPath)
if(-Not $VSInstall) {
    throw "Unable to locate VS installation"
}

$MSBuild = "$VSInstall\MSBuild\Current\Bin\MSBuild.exe"

if(-Not $env:NUGET_VERSION) {
    throw "NUGET_VERSION not defined, aborting..."
}

if(-Not $env:API_KEY) {
    throw "API_KEY not defined, aborting..."
}

if(-Not $env:NUGET_REPO) {
    throw "NUGET_REPO not defined, aborting..."
}

if($env:WORKSPACE) {
    Copy-Item "$env:WORKSPACE\product-texts\mobile\couchbase-lite\license\LICENSE_community.txt" "$PSScriptRoot\LICENSE.txt"
}

Write-Host
Write-Host *** RESTORING DEP PACKAGES ***
Write-Host

& $MSBuild /t:Restore $PSScriptRoot\..\..\src\Couchbase.Lite.sln

Write-Host
Write-Host *** PACKING ***
Write-Host

& $MSBuild $PSScriptRoot\..\..\src\Couchbase.Lite.sln /t:Pack /p:Configuration=Packaging /p:Version=$env:NUGET_VERSION

# Workaround the inability to pin a version of a ProjectReference in csproj
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
Remove-Item -Force -Recurse $PSScriptRoot\..\..\src\packages\tmp -ErrorAction Ignore
New-Item -ItemType Directory $PSScriptRoot\..\..\src\packages\tmp
[System.IO.Compression.ZipFile]::ExtractToDirectory("$PSScriptRoot\..\..\src\packages\Couchbase.Lite.$env:NUGET_VERSION.nupkg", `
    "$PSScriptRoot\..\..\src\packages\tmp")
$nuspecContent = $(Get-Content -Path "$PSScriptRoot\..\..\src\packages\tmp\Couchbase.Lite.nuspec"). `
    Replace("version=`"$env:NUGET_VERSION`"", "version=`"[${env:NUGET_VERSION}]`"")
Set-Content -Path "$PSScriptRoot\..\..\src\packages\tmp\Couchbase.Lite.nuspec" $nuspecContent
Remove-Item -Path "$PSScriptRoot\..\..\src\packages\Couchbase.Lite.$env:NUGET_VERSION.nupkg" -ErrorAction Ignore -Force
& 7z a -tzip "$PSScriptRoot\..\..\src\packages\Couchbase.Lite.$env:NUGET_VERSION.nupkg" "$PSScriptRoot\..\..\src\packages\tmp\*"
Remove-Item -Force -Recurse $PSScriptRoot\..\..\src\packages\tmp
# End Workaround

Get-ChildItem "$PSScriptRoot\..\..\src\packages" -Filter *.nupkg |
ForEach-Object {
    dotnet nuget push $_.FullName --disable-buffering  --api-key $env:API_KEY --source $env:NUGET_REPO
    if($LASTEXITCODE) {
        throw "Failed to push $_"
    }
}