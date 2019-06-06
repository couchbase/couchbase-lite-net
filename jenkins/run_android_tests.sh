export PATH=/usr/local/bin:$PATH
if [ -d couchbase-lite-net ]; then
	cd couchbase-lite-net
    git fetch origin
    git reset --hard
    git checkout $BRANCH
    git clean -dfx src/Couchbase.Lite.Tests.Android
    git pull origin $BRANCH
else
	git clone https://github.com/couchbase/couchbase-lite-net
    cd couchbase-lite-net
    git checkout $BRANCH
fi

~/Library/Developer/Xamarin/android-sdk-macosx/tools/emulator -avd Nexus_S_API_26 -netdelay none -netspeed full &

touch src/Couchbase.Lite/Properties/version
git submodule update --init # recursive not needed here

assemblyVersion=`echo $VERSION | awk -F "-" '{print $1}'`
nugetVersion=`echo $VERSION | awk -F "-" '{print $2}'`

cd vendor/couchbase-lite-core
export sha=`git rev-parse HEAD`
cd ../../src

cd Couchbase.Lite.Tests.Android
ipconfig getifaddr en1 > result_ip
# Test the debug build
#/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /t:Restore /p:RestoreSources=http://mobile.nuget.couchbase.com/nuget/CI/%3bhttps://api.nuget.org/v3/index.json Couchbase.Lite.Tests.Android.sln
#/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /p:Configuration=Debug /p:SourceLinkCreate=false /t:SignAndroidPackage Couchbase.Lite.Tests.Android.sln
#pushd ../../Tools/Touch.Server
#/Library/Frameworks/Mono.framework/Versions/Current/Commands/mcs Main.cs Options.cs -Out:Touch.Server.exe
#/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono Touch.Server.exe -p 54321 &
#my_pid=$!
#popd
#~/Library/Developer/Xamarin/android-sdk-macosx/platform-tools/adb -s emulator-5554 install -r bin/Debug/Couchbase.Lite.Tests.Android-Signed.apk
#~/Library/Developer/Xamarin/android-sdk-macosx/platform-tools/adb -s emulator-5554 shell am start Couchbase.Lite.Tests.Android/test.activity
#wait $my_pid || exit 1

/usr/local/bin/dos2unix modify_packages.sh
chmod 777 modify_packages.sh
./modify_packages.sh $assemblyVersion $nugetVersion
/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /t:Restore /p:RestoreSources=http://mobile.nuget.couchbase.com/nuget/CI/%3bhttps://api.nuget.org/v3/index.json Couchbase.Lite.Tests.Android.csproj
/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild /p:Configuration=Release /t:SignAndroidPackage Couchbase.Lite.Tests.Android.csproj
    
pushd ../../Tools/Touch.Server
#/Library/Frameworks/Mono.framework/Versions/Current/Commands/mcs Main.cs Options.cs -Out:Touch.Server.exe
/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono Touch.Server.exe -p 54321 &
my_pid=$!
popd
~/Library/Developer/Xamarin/android-sdk-macosx/platform-tools/adb -s emulator-5554 install -r bin/Release/Couchbase.Lite.Tests.Android-Signed.apk
~/Library/Developer/Xamarin/android-sdk-macosx/platform-tools/adb -s emulator-5554 shell am start Couchbase.Lite.Tests.Android/test.activity
wait $my_pid
retcode=$?
~/Library/Developer/Xamarin/android-sdk-macosx/platform-tools/adb -s emulator-5554 emu kill
exit $retcode