[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bOR [Net.SecurityProtocolType]::Tls12
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

if($env:WORKSPACE) {
    Copy-Item "$env:WORKSPACE\product-texts\mobile\couchbase-lite\license\LICENSE_community.txt" "$PSScriptRoot\LICENSE.txt"
}

# Only do snupkg for the main package, since the support ones can contain 
# native pdb which will be rejected by nuget.org
..\..\nuget.exe pack couchbase-lite.nuspec -Properties version=$env:NUGET_VERSION -BasePath ..\..\ -Symbols -SymbolPackageFormat snupkg

Get-ChildItem "." -Filter *support*.nuspec |
ForEach-Object {
    ..\..\nuget.exe pack $_.Name -Properties version=$env:NUGET_VERSION -BasePath ..\..\
    if($LASTEXITCODE) {
        Pop-Location
        throw "Failed to package $_"
    }
}

Get-ChildItem "." -Filter *.nupkg |
ForEach-Object {
    ..\..\nuget.exe push -DisableBuffering $_.Name $env:API_KEY -Source $env:NUGET_REPO
    if($LASTEXITCODE) {
        Pop-Location
        throw "Failed to push -DisableBuffering $_"
    }
}

Pop-Location