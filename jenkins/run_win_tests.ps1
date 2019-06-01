if(Test-Path couchbase-lite-net) {
    cd couchbase-lite-net
    & 'C:\Program Files\Git\bin\git.exe' fetch origin
    & 'C:\Program Files\Git\bin\git.exe' reset --hard
    & 'C:\Program Files\Git\bin\git.exe' checkout $env:BRANCH
    & 'C:\Program Files\Git\bin\git.exe' clean -dfx .
    & 'C:\Program Files\Git\bin\git.exe' pull origin $env:BRANCH
} else {
    & 'C:\Program Files\Git\bin\git.exe' clone https://github.com/couchbase/couchbase-lite-net
    cd couchbase-lite-net
    & 'C:\Program Files\Git\bin\git.exe' checkout $env:BRANCH
}

echo "" | Out-File src/Couchbase.Lite/Properties/version -Encoding ASCII -NoNewLine
& 'C:\Program Files\Git\bin\git.exe' submodule update --init # recursive not needed here

pushd vendor/couchbase-lite-core
$sha=$(& 'C:\Program Files\Git\bin\git.exe' rev-parse HEAD)
popd

cd src/Couchbase.Lite.Tests.NetCore
$sourceProjectFile = "Couchbase.Lite.Tests.NetCore.Source.csproj"
if (-NOT (Test-Path $sourceProjectFile)) {
    # Test the debug build
    dotnet nuget locals http-cache --clear
    .\modify_packages.ps1 -Version $Env:VERSION
    dotnet restore --no-cache -s https://api.nuget.org/v3/index.json -s http://mobile.nuget.couchbase.com/nuget/CI/ Couchbase.Lite.Tests.NetCore.csproj
    dotnet test -v n --framework netcoreapp2.0 --logger "trx;LogFileName=unit_tests.xml" 
    if ($lastexitcode -ne 0) {
        throw "Debug testing failed"
    }

    # Test the release package
    dotnet restore --no-cache -s https://api.nuget.org/v3/index.json -s http://mobile.nuget.couchbase.com/nuget/CI/ Couchbase.Lite.Tests.NetCore.csproj /p:Configuration=Release
    dotnet test -v n -c Release --framework netcoreapp2.0 --logger "trx;LogFileName=unit_tests.xml" 
    if ($lastexitcode -ne 0) {
        throw "Release testing failed"
    }
} else {
    ../build/do_fetch_litecore.ps1 -DebugLib -Variants windows-win32,windows-win64 -NexusRepo $env:NEXUS_REPO -Sha $sha
    # Test the debug build (bug in dotnet-xunit prevents having more than one
    # csproj file in the directory, so temporarily rename the other as a hack)
    mv Couchbase.Lite.Tests.NetCore.csproj Couchbase.Lite.Tests.NetCore.foo
    dotnet restore Couchbase.Lite.Tests.NetCore.Source.csproj
    dotnet restore ../Couchbase.Lite/Couchbase.Lite.csproj
    dotnet restore ../Couchbase.Lite.Support.NetDesktop/Couchbase.Lite.Support.NetDesktop.csproj
    dotnet test -v normal --framework netcoreapp2.0 --no-restore Couchbase.Lite.Tests.NetCore.Source.csproj --logger "trx;LogFileName=unit_tests.xml" 
    if ($lastexitcode -ne 0) {
        throw "Debug testing failed"
    }

    # Test the release package
    mv Couchbase.Lite.Tests.NetCore.foo Couchbase.Lite.Tests.NetCore.csproj
    mv Couchbase.Lite.Tests.NetCore.Source.csproj Couchbase.Lite.Tests.NetCore.Source.foo
    .\modify_packages.ps1 -Version $Env:VERSION
    dotnet nuget locals http-cache --clear
    dotnet restore -s https://api.nuget.org/v3/index.json -s http://mobile.nuget.couchbase.com/nuget/CI/ Couchbase.Lite.Tests.NetCore.csproj
    dotnet test -v normal --framework netcoreapp2.0 --no-restore -c Release Couchbase.Lite.Tests.NetCore.csproj --logger "trx;LogFileName=unit_tests.xml" 
    if ($lastexitcode -ne 0) {
        throw "Release testing failed"
    }
}