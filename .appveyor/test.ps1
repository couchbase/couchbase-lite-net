dotnet tool install coveralls.net --version 1.0.0 --tool-path src\Couchbase.Lite.TestCoverage\coveralls
Push-Location $PSScriptRoot\..\src\Couchbase.Lite.TestCoverage\
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
Pop-Location