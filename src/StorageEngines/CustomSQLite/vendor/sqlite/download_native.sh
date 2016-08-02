#!/bin/bash
pushd `dirname $0`
mkdir arm64-v8a armeabi armeabi-v7a x86 x86_64 x64 2> /dev/null
ANDROID_FILENAMES=(arm64-v8a/libsqlite3.so armeabi/libsqlite3.so armeabi-v7a/libsqlite3.so x86/libsqlite3.so x86_64/libsqlite3.so)
for filename in ${ANDROID_FILENAMES[@]}; do
	final_filename=${filename/sqlite/cbsqlite}
  if [ ! -f $final_filename ]; then
    echo "Downloading $filename (Android)"
    curl -L https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/android/$filename -o $final_filename
  fi
done

if [ ! -f libsqlite3.a ]; then
  echo "Downloading libsqlite3.a (iOS)"
  curl -LO https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/ios/libsqlite3.a
fi

if [ ! -f libcbsqlite3.so ]; then
  echo "Downloading libsqlite.so (Linux 64-bit)"
  curl -L https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/linux/x86_64/libsqlite3.so -o libcbsqlite3.so
fi

if [ ! -f libcbsqlite3.dylib ]; then
  echo "Downloading libsqlite3.dylib (OS X)"
  curl -L https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/osx/libsqlite3.dylib -o libcbsqlite3.dylib
fi

if [ ! -f x86/cbsqlite3.dll ]; then
  echo "Downloading x86/sqlite3.dll (Windows)"
  curl -L https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/windows/x86/sqlite3.dll -o x86/cbsqlite3.dll
fi

if [ ! -f x64/cbsqlite3.dll ]; then
  echo "Downloading x64/sqlite3.dll (Windows)"
  curl -L https://github.com/couchbase/couchbase-lite-java-native/raw/master/vendor/sqlite/libs/windows/x86_64/sqlite3.dll -o x64/cbsqlite3.dll
fi
popd
