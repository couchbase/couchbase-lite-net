export PATH=/usr/local/bin:$PATH
if [ -d couchbase-lite-net ]; then
	cd couchbase-lite-net
    git fetch origin
    git reset --hard
    git checkout $BRANCH
    git clean -dfx src/Couchbase.Lite.Tests.iOS
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
powershell build/do_fetch_litecore.ps1 -DebugLib -Variants ios -NexusRepo $NEXUS_REPO -Sha $sha
mkdir -p Couchbase.Lite.Support.Apple/iOS/Native/
rm -rf Couchbase.Lite.Support.Apple/iOS/Native/LiteCore.framework
cp -R ../vendor/couchbase-lite-core/build_cmake/ios-fat/LiteCore.framework Couchbase.Lite.Support.Apple/iOS/Native/LiteCore.framework
cd Couchbase.Lite.Tests.iOS
ipconfig getifaddr en1 > result_ip

# Test the debug build
/usr/local/bin/dos2unix modify_packages.sh
chmod 777 modify_packages.sh
./modify_packages.sh $assemblyVersion $nugetVersion
/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /t:Restore /p:RestoreSources="http://mobile.nuget.couchbase.com/nuget/CI/%3bhttps://api.nuget.org/v3/index.json" Couchbase.Lite.Tests.iOS.csproj
/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /p:Configuration=Debug /p:Platform=iPhoneSimulator /p:SourceLinkCreate=false Couchbase.Lite.Tests.iOS.sln
security -v unlock-keychain -p Passw0rd $HOME/Library/Keychains/login.keychain-db
pushd ../../Tools/Touch.Server
/Library/Frameworks/Mono.framework/Versions/Current/Commands/mcs Main.cs Options.cs -Out:Touch.Server.exe
/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono Touch.Server.exe &
my_pid=$!
popd
pushd bin/iPhoneSimulator/Debug
/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/bin/mtouch -sdkroot /Applications/Xcode.app -launchsim=Couchbase.Lite.Tests.iOS.app -device=:v2:runtime=com.apple.CoreSimulator.SimRuntime.iOS-12-1,devicetype=com.apple.CoreSimulator.SimDeviceType.iPhone-7
wait $my_pid || exit 1
popd

#Test the release build
pushd ../../Tools/Touch.Server
/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono Touch.Server.exe &
my_pid=$!
popd
/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /t:Restore /p:RestoreSources="http://mobile.nuget.couchbase.com/nuget/CI/%3bhttps://api.nuget.org/v3/index.json" Couchbase.Lite.Tests.iOS.csproj
/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /p:Configuration=Release /p:Platform=iPhoneSimulator /p:SourceLinkCreate=false Couchbase.Lite.Tests.iOS.sln
pushd bin/iPhoneSimulator/Release
/Library/Frameworks/Xamarin.iOS.framework/Versions/Current/bin/mtouch -sdkroot /Applications/Xcode.app -launchsim=Couchbase.Lite.Tests.iOS.app -device=:v2:runtime=com.apple.CoreSimulator.SimRuntime.iOS-12-1,devicetype=com.apple.CoreSimulator.SimDeviceType.iPhone-7
wait $my_pid || exit 1
popd