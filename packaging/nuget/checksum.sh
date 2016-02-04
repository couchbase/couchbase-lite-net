#!/bin/sh

shasum tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/MonoAndroid/arm64-v8a/libCBForest-Interop.so
shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/arm64-v8a/libCBForest-Interop.so 

shasum /Users/borrrden/Development/couchbase-lite-net/packaging/nuget/tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/MonoAndroid/armeabi-v7a/libCBForest-Interop.so 
shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/armeabi-v7a/libCBForest-Interop.so 

shasum /Users/borrrden/Development/couchbase-lite-net/packaging/nuget/tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/MonoAndroid/x86/libCBForest-Interop.so 
shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/x86/libCBForest-Interop.so 

shasum /Users/borrrden/Development/couchbase-lite-net/packaging/nuget/tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/MonoAndroid/x86_64/libCBForest-Interop.so 
shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/x86_64/libCBForest-Interop.so 

shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/CBForest-Interop.dll 
shasum /Users/borrrden/Development/couchbase-lite-net/packaging/nuget/tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/net35/CBForest-Interop.dll 

shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/libCBForest-Interop.a
monodis --mresources /Users/borrrden/Development/couchbase-lite-net/packaging/nuget/tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/Xamarin.iOS10/cbforest-sharp.dll 
shasum libCBForest-Interop.a

shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/libCBForest-Interop.dylib 
shasum /Users/borrrden/Development/couchbase-lite-net/packaging/nuget/tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/net35/libCBForest-Interop.dylib 

shasum /Users/borrrden/Development/couchbase-lite-net/src/Couchbase.Lite.Shared/vendor/cbforest/CSharp/prebuilt/libCBForest-Interop.so 
shasum /Users/borrrden/Development/couchbase-lite-net/packaging/nuget/tmp/Couchbase.Lite.Storage.ForestDB.1.2/build/net35/libCBForest-Interop.so 
