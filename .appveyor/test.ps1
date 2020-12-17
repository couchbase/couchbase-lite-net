dotnet tool install coveralls.net --version 2.0.0 --tool-path src\Couchbase.Lite.TestCoverage\coveralls
Push-Location $PSScriptRoot\..\src\Couchbase.Lite.TestCoverage\
Push-Location bin\Debug\netcoreapp2.1
& ..\..\..\packages\OpenCover.4.7.922\tools\OpenCover.Console.exe -returntargetcode -register:user -target:dotnet.exe "-targetargs:""..\..\..\packages\xunit.runner.console.2.4.1\tools\netcoreapp2.0\xunit.console.dll"" "".\Couchbase.Lite.TestCoverage.dll"" -noshadow -appveyor"  -oldStyle -output:opencoverCoverage.xml -filter:"+[Couchbase.Lite]* -[Couchbase.Lite]LiteCore.Interop.*" -excludebyattribute:"*.ExcludeFromCodeCoverageAttribute" 2> $null;
if($LASTEXITCODE -ne 0) {
    Pop-Location
    exit $LASTEXITCODE
}

Pop-Location
if (-not (Test-Path env:APPVEYOR_PULL_REQUEST_NUMBER)) {
    & "coveralls\csmacnz.coveralls.exe" --opencover -i "bin\Debug\netcoreapp2.1\opencoverCoverage.xml"
    Pop-Location
    exit $LASTEXITCODE
}

Pop-Location