Push-Location $PSScriptRoot
Remove-Item *.nupkg
if(-Not (Test-Path ..\..\nuget.exe)) {
    Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile ..\..\nuget.exe
}

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

Get-ChildItem "." -Filter *.nuspec |
ForEach-Object {
    ..\..\nuget.exe pack $_.Name /Properties version=$env:NUGET_VERSION /BasePath ..\..\
    if($LASTEXITCODE) {
        Pop-Location
        throw "Failed to package $_"
    }
}

Get-ChildItem "." -Filter *.nupkg |
ForEach-Object {
    ..\..\nuget.exe push $_.Name $env:API_KEY -Source $env:NUGET_REPO
    if($LASTEXITCODE) {
        Pop-Location
        throw "Failed to push $_"
    }
}

Pop-Location