export PATH=/usr/local/bin:$PATH
if [ -d couchbase-lite-net ]; then
	cd couchbase-lite-net
    git fetch origin
    git reset --hard
    git checkout $BRANCH
    git clean -dfx .
    git pull origin $BRANCH
else
	git clone https://github.com/couchbase/couchbase-lite-net
    cd couchbase-lite-net
    git checkout $BRANCH
fi

touch src/Couchbase.Lite/Properties/version
git submodule update --init # recursive not needed here
assemblyVersion=`echo $VERSION | awk -F "-" '{print $1}'`
nugetVersion=`echo $VERSION | awk -F "-" '{print $2}'`

pushd vendor/couchbase-lite-core
export sha=`git rev-parse HEAD`
popd
cd src



# Enable crash dumps
ulimit -c unlimited

if [ ! -f Couchbase.Lite.Tests.NetCore/Couchbase.Lite.Tests.NetCore.Source.csproj ];
then
	cd Couchbase.Lite.Tests.NetCore
    # Test the debug package
    /usr/local/bin/dos2unix modify_packages.sh
    chmod 777 modify_packages.sh
    ./modify_packages.sh $assemblyVersion $nugetVersion
    /usr/local/share/dotnet/dotnet nuget locals http-cache --clear
    /usr/local/share/dotnet/dotnet restore -s http://mobile.nuget.couchbase.com/nuget/CI/ -s https://api.nuget.org/v3/index.json Couchbase.Lite.Tests.NetCore.csproj
    /usr/local/share/dotnet/dotnet test -v n --no-restore

    # Test the release package
    /usr/local/share/dotnet/dotnet test -c Release -v n --no-restore
else
	powershell build/do_fetch_litecore.ps1 -DebugLib -Variants macosx -NexusRepo $NEXUS_REPO -Sha $sha
	cd Couchbase.Lite.Tests.NetCore
    
    # Test the debug build (bug in dotnet-xunit prevents having more than one
    # csproj file in the directory, so temporarily rename the other as a hack)
    mv Couchbase.Lite.Tests.NetCore.csproj Couchbase.Lite.Tests.NetCore.foo
    /usr/local/share/dotnet/dotnet restore Couchbase.Lite.Tests.NetCore.Source.csproj
    /usr/local/share/dotnet/dotnet restore ../Couchbase.Lite/Couchbase.Lite.csproj
    /usr/local/share/dotnet/dotnet restore ../Couchbase.Lite.Support.NetDesktop/Couchbase.Lite.Support.NetDesktop.csproj
    /usr/local/share/dotnet/dotnet test -v n --no-restore Couchbase.Lite.Tests.NetCore.Source.csproj

    # Test the release package
    mv Couchbase.Lite.Tests.NetCore.foo Couchbase.Lite.Tests.NetCore.csproj
    mv Couchbase.Lite.Tests.NetCore.Source.csproj Couchbase.Lite.Tests.NetCore.Source.foo
    /usr/local/bin/dos2unix modify_packages.sh
    chmod 777 modify_packages.sh
    ./modify_packages.sh $assemblyVersion $nugetVersion
    /usr/local/share/dotnet/dotnet nuget locals http-cache --clear
    /usr/local/share/dotnet/dotnet restore -s http://mobile.nuget.couchbase.com/nuget/CI/ -s https://api.nuget.org/v3/index.json Couchbase.Lite.Tests.NetCore.csproj
    /usr/local/share/dotnet/dotnet test -c Release -v n --no-restore Couchbase.Lite.Tests.NetCore.csproj
fi