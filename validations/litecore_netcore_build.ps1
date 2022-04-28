# .Net Core PR Validation

# Move LiteCore EE
Move-Item -Path couchbase-lite-core-EE -Destination couchbase-lite-net/vendor

# Build LiteCore
New-Item -Type Directory couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/x86/RelWithDebInfo
Push-Location couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/x86/RelWithDebInfo
New-Item -Type File LiteCore.dll
New-Item -Type File LiteCore.pdb
Pop-Location

New-Item -Type Directory couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/x64
Push-Location couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/x64
cmake -G "Visual Studio 16 2019" -A x64 -DBUILD_ENTERPRISE=ON ..\..
cmake --build . --target LiteCore --config RelWithDebInfo
Pop-Location