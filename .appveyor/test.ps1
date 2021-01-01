Push-Location $PSScriptRoot\..\src\Couchbase.Lite.TestCoverage\
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
if($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not (Test-Path env:APPVEYOR_PULL_REQUEST_NUMBER)) {
    Pop-Location
    exit $LASTEXITCODE
}

Pop-Location