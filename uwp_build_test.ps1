# UWP PR Validation

# Submodule
git submodule update --init --recursive

# Build LiteCore
New-Item -Type Directory vendor/couchbase-lite-core/build_cmake/x86_store/RelWithDebInfo
Push-Location vendor/couchbase-lite-core/build_cmake/x86_store/RelWithDebInfo
New-Item -Type File LiteCore.dll
New-Item -Type File LiteCore.pdb
Pop-Location

New-Item -Type Directory vendor/couchbase-lite-core/build_cmake/x64_store
Push-Location vendor/couchbase-lite-core/build_cmake/x64_store
cmake -G "Visual Studio 16 2019" -A x64 -DCMAKE_SYSTEM_NAME=WindowsStore -DCMAKE_SYSTEM_VERSION='10.0' ..\..
cmake --build . --target LiteCore --config RelWithDebInfo
Pop-Location

# Submodule
#git submodule update --init # (--recursive) recursive not needed here 

# Fetch LiteCore
#pushd vendor/couchbase-lite-core
#$sha=$(& git rev-parse HEAD)
#popd
#cd src
#$NEXUS_REPO='http://nexus.build.couchbase.com:8081/nexus/content/repositories/releases/com/couchbase/litecore/'
#build/do_fetch_litecore.ps1 -Variants windows-win32,windows-win64 -NexusRepo $NEXUS_REPO -Sha $sha