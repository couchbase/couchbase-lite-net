Push-Location $PSScriptRoot\..\src\Couchbase.Lite.TestCoverage\
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
Pop-Location