#/bin/sh

rm -rf ../../src/Couchbase.Lite.Net45/bin
rm -rf ../../src/Couchbase.Lite.Net45/obj
rm -rf ../../src/Couchbase.Lite.Net35/bin
rm -rf ../../src/Couchbase.Lite.Net35/obj
rm -rf ../../src/Couchbase.Lite.iOS/bin
rm -rf ../../src/Couchbase.Lite.iOS/obj
rm -rf ../../src/Couchbase.Lite.Android/bin
rm -rf ../../src/Couchbase.Lite.Android/obj
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.Net35/bin
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.Net35/obj
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.Net45/bin
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.Net45/obj
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.iOS/bin
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.iOS/obj
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.Android/bin
rm -rf ../../src/ListenerComponent/Couchbase.Lite.Listener.Android/obj
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Net35/bin
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Net35/obj
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Net45/bin
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Net45/obj
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.iOS/bin
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.iOS/obj
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Android/bin
rm -rf ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Android/obj
rm -rf ../../src/StorageEngines/SystemSQLite/storage.systemsqlite.net35/bin
rm -rf ../../src/StorageEngines/SystemSQLite/storage.systemsqlite.ios/bin
rm -rf ../../src/StorageEngines/SystemSQLite/storage.systemsqlite.droid/bin
rm -rf ../../src/StorageEngines/SystemSQLite/storage.systemsqlite.net35/obj
rm -rf ../../src/StorageEngines/SystemSQLite/storage.systemsqlite.ios/obj
rm -rf ../../src/StorageEngines/SystemSQLite/storage.systemsqlite.droid/obj
rm -rf ../../src/StorageEngines/SQLCipher/storage.sqlcipher.net35/bin
rm -rf ../../src/StorageEngines/SQLCipher/storage.sqlcipher.ios/bin
rm -rf ../../src/StorageEngines/SQLCipher/storage.sqlcipher.droid/bin
rm -rf ../../src/StorageEngines/SQLCipher/storage.sqlcipher.net35/obj
rm -rf ../../src/StorageEngines/SQLCipher/storage.sqlcipher.ios/obj
rm -rf ../../src/StorageEngines/SQLCipher/storage.sqlcipher.droid/obj
rm -rf ../../src/StorageEngines/ForestDB/storage.forestdb.net35/bin
rm -rf ../../src/StorageEngines/ForestDB/storage.forestdb.ios/bin
rm -rf ../../src/StorageEngines/ForestDB/storage.forestdb.droid/bin
rm -rf ../../src/StorageEngines/ForestDB/storage.forestdb.net35/obj
rm -rf ../../src/StorageEngines/ForestDB/storage.forestdb.ios/obj
rm -rf ../../src/StorageEngines/ForestDB/storage.forestdb.droid/obj

xbuild /p:Configuration=Release ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Net35/Couchbase.Lite.Listener.Bonjour.Net35.csproj
xbuild /p:Configuration=Release ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Net45/Couchbase.Lite.Listener.Bonjour.Net45.csproj
xbuild /p:Configuration=Release ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.iOS/Couchbase.Lite.Listener.Bonjour.iOS.csproj
xbuild /p:Configuration=Release ../../src/ListenerComponent/Bonjour/Couchbase.Lite.Listener.Bonjour.Android/Couchbase.Lite.Listener.Bonjour.Android.csproj

LITE_NUGET_DIR="../../src/Couchbase.Lite.Net35/bin/Nuget/"
LITE_RELEASE_DIR="../../src/Couchbase.Lite.Net35/bin/Release"
LITE_RELEASE_FILES="$LITE_RELEASE_DIR/Couchbase.Lite.*
$LITE_RELEASE_DIR/ICSharpCode.SharpZipLib.dll
$LITE_RELEASE_DIR/Microsoft.Scripting.Core.dll
$LITE_RELEASE_DIR/Rackspace.Threading.dll*
$LITE_RELEASE_DIR/Stateless.dll
$LITE_RELEASE_DIR/SQLitePCL.raw.dll
$LITE_RELEASE_DIR/System.Net.Http.Net35.dll*
$LITE_RELEASE_DIR/System.Threading.Tasks.Net35.dll*
$LITE_RELEASE_DIR/ugly_net35.dll
$LITE_RELEASE_DIR/cbforest-sharp.dll*"

LITE_LISTENER_NUGET_DIR="../../src/ListenerComponent/Couchbase.Lite.Listener.Net35/bin/Nuget/"
LITE_LISTENER_RELEASE_DIR="../../src/ListenerComponent/Couchbase.Lite.Listener.Net35/bin/Release"
LITE_LISTENER_RELEASE_FILES="$LITE_LISTENER_RELEASE_DIR/Couchbase.Lite.Listener.dll*
$LITE_LISTENER_RELEASE_DIR/Jint.dll"

rm -rf $LITE_NUGET_DIR
mkdir -p $LITE_NUGET_DIR
for f in $LITE_RELEASE_FILES
do
  cp $f $LITE_NUGET_DIR
done

rm -rf $LITE_LISTENER_NUGET_DIR
mkdir -p $LITE_LISTENER_NUGET_DIR
for f in $LITE_LISTENER_RELEASE_FILES
do
  cp $f $LITE_LISTENER_NUGET_DIR
done

nuget pack -BasePath ../.. couchbase-lite.nuspec 
nuget pack -BasePath ../.. couchbase-lite-listener.nuspec 
nuget pack -BasePath ../.. couchbase-lite-listener-bonjour.nuspec 
nuget pack -BasePath ../.. couchbase-lite-storage-systemsqlite.nuspec
nuget pack -BasePath ../.. couchbase-lite-storage-sqlcipher.nuspec
nuget pack -BasePath ../.. couchbase-lite-storage-forestdb.nuspec
