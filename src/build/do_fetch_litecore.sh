#!/bin/bash

function usage() {
    echo "usage: do_fetch_litecore -v <VAL> [-d]"
    echo "  -v|--variants <VAL>     The comma separated list of platform IDs to download"
    echo "  -d|--debug              If set, download the debug versions of the binaries"
    echo
}

VARIANTS="$2"
DEBUG_LIB=""
if [ "$3" = "--debug-lib" ]; then
    DEBUG_LIB="-d"
fi

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null 2>&1 && pwd )"
pushd $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake
Sha=`"$SCRIPT_DIR/amalgamate_sha.sh"`

if [[ "$OSTYPE" != "darwin"* ]]; then
python3 -m venv myenv
pip3 install GitPython
fi

python3 $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/tools/fetch_litecore.py -v $VARIANTS $DEBUG_LIB -s $Sha -o $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake

if [ -f "macos/x86_64/lib/libLiteCore.dylib" ]; then
    mv -f macos/x86_64/lib/libLiteCore.dylib libLiteCore.dylib
    rm -rf macos
fi

if [ -f "linux/x86_64/lib/libLiteCore.so" ]; then
    mv -f linux/x86_64/lib/libLiteCore.so libLiteCore.so
	mv -f linux/x86_64/lib/libstdc++.so.6 libstdc++.so.6
    mv -f linux/x86_64/lib/libstdc++.so libstdc++.so
    mv -f linux/x86_64/lib/libicudata.so.54 libicudata.so.54
    mv -f linux/x86_64/lib/libicui18n.so.54 libicui18n.so.54
    mv -f linux/x86_64/lib/libicuuc.so.54 libicuuc.so.54
	cd linux
    rm -rf x86_64
	cd ..
fi

if [ -f "ios/LiteCore.framework/LiteCore" ]; then
    mkdir -p $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/ios-fat/LiteCore.framework
    cp -a $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/ios/LiteCore.framework/* $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/ios-fat/LiteCore.framework/
	rm -rf ios
fi

ANDROID_ARCHS=("x86" "x86_64" "armeabi-v7a" "arm64-v8a")
for arch in ${ANDROID_ARCHS[@]}; do
    if [ -f "android/$arch/lib/libLiteCore.so" ]; then
		mkdir -p $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/android/lib/$arch
		cp -a $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/android/$arch/lib/libLiteCore.so $SCRIPT_DIR/../couchbase-lite-net/vendor/couchbase-lite-core/build_cmake/android/lib/$arch/
        cd android
		rm -rf $arch
        cd ..
    fi
done