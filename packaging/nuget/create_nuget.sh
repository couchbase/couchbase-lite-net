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

xbuild /p:Configuration=Release ../../src/Couchbase.Lite.Net35.sln
xbuild /p:Configuration=Release ../../src/Couchbase.Lite.Net45.sln
xbuild /p:Configuration=Release ../../src/Couchbase.Lite.iOS.sln
xbuild /p:Configuration=Release ../../src/Couchbase.Lite.Android.sln

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
$LITE_RELEASE_DIR/ugly_net35.dll"

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
