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
cd src
#build/do_fetch_litecore.ps1 -DebugLib -Variants windows-x86_store,windows-x64_store,windows-arm -NexusRepo $env:NEXUS_REPO -Sha $sha
cd Couchbase.Lite.Tests.UWP

if(![System.IO.File]::Exists("nuget.exe")) { 
    Invoke-WebRequest https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -OutFile nuget.exe 
} 

$sourceProjectFile = "FromSource/Couchbase.Lite.Tests.UWP.Source.csproj"
if (-NOT (Test-Path $sourceProjectFile)) {
    .\nuget.exe locals http-cache -clear
    .\modify_packages.ps1 -Version $Env:VERSION
    & 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe' --% /t:Restore /p:RestoreSources="http://mobile.nuget.couchbase.com/nuget/CI/;https://api.nuget.org/v3/index.json" Couchbase.Lite.Tests.UWP.csproj
    .\nuget.exe locals http-cache -clear
    # Prepare the release build
    & 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe' --% /t:Restore /p:RestoreSources="http://mobile.nuget.couchbase.com/nuget/CI/;https://api.nuget.org/v3/index.json" Couchbase.Lite.Tests.UWP.csproj /p:Configuration=Release
} else {
    # Prepare the debug build
    # pushd FromSource
    .\nuget.exe locals http-cache -clear
    # & 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe' --% /t:Restore Couchbase.Lite.Tests.UWP.Source.csproj
    # popd

    # Prepare the release build
    .\modify_packages.ps1 -Version $Env:VERSION
    & 'C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\MSBuild.exe' --% /t:Restore /p:RestoreSources="http://mobile.nuget.couchbase.com/nuget/CI/;https://api.nuget.org/v3/index.json" Couchbase.Lite.Tests.UWP.csproj
}

